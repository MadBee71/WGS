using System.Collections.Concurrent;
using System.Net.Sockets;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Monitors running servers for freezes: the OS process is alive but the game
/// no longer responds to A2S/REST queries.  After N consecutive failed health
/// checks it takes the configured action (notify / restart).
/// </summary>
public class ServerHealthService : IDisposable
{
    private readonly ServerManagerService _manager;
    private readonly NotificationService  _notifications;
    private readonly ConfigService        _config;

    // serverId -> consecutive failure count
    private readonly ConcurrentDictionary<string, int> _failures = new();

    private System.Timers.Timer? _timer;

    // servers under active health-check surveillance
    private List<(GameServer Server, IGamePlugin Plugin)> _watched = [];
    private readonly object _watchLock = new();

    public ServerHealthService(ServerManagerService manager,
                               NotificationService notifications,
                               ConfigService config)
    {
        _manager       = manager;
        _notifications = notifications;
        _config        = config;

        manager.StatusChanged += OnStatusChanged;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartMonitoring()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = new System.Timers.Timer(60_000); // check every 60 s
        _timer.Elapsed  += (_, _) => _ = CheckAllAsync();
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    // ── Status tracking ───────────────────────────────────────────────────────

    private void OnStatusChanged(string serverId, ServerStatus status)
    {
        var server = _manager.GetServer(serverId);
        if (server == null) return;
        var plugin = GameRegistry.All.FirstOrDefault(p => p.GameId == server.GameId);
        if (plugin == null) return;

        lock (_watchLock)
        {
            _watched = _watched.Where(w => w.Server.Id != serverId).ToList();

            if (status == ServerStatus.Running && CanHealthCheck(server, plugin))
            {
                _watched.Add((server, plugin));
                _failures[serverId] = 0;
            }
            else
            {
                _failures.TryRemove(serverId, out _);
            }
        }
    }

    // ── Health check loop ─────────────────────────────────────────────────────

    private async Task CheckAllAsync()
    {
        if (!_config.HealthCheckEnabled) return;

        List<(GameServer Server, IGamePlugin Plugin)> snapshot;
        lock (_watchLock) snapshot = [.. _watched];

        foreach (var (server, plugin) in snapshot)
        {
            if (server.Status != ServerStatus.Running) continue;
            bool alive = await IsRespondingAsync(server, plugin);

            if (alive)
            {
                _failures[server.Id] = 0;
            }
            else
            {
                int count = _failures.AddOrUpdate(server.Id, 1, (_, c) => c + 1);
                int threshold = Math.Max(1, _config.HealthCheckFailThreshold);

                if (count >= threshold)
                {
                    _failures[server.Id] = 0;
                    await TriggerFreezeActionAsync(server);
                }
            }
        }
    }

    private static async Task<bool> IsRespondingAsync(GameServer server, IGamePlugin plugin)
    {
        try
        {
            if (plugin is IA2SQueryPlugin a2s)
            {
                // A2S INFO query — more reliable than player query for freeze detection
                using var udp = new UdpClient();
                udp.Client.ReceiveTimeout = 3000;
                udp.Client.SendTimeout    = 3000;
                byte[] infoReq = [0xFF, 0xFF, 0xFF, 0xFF, 0x54,
                    .. System.Text.Encoding.UTF8.GetBytes("Source Engine Query\0")];
                var ep = new System.Net.IPEndPoint(
                    System.Net.IPAddress.Parse(a2s.A2SHost), a2s.GetA2SPort(server));
                await udp.SendAsync(infoReq, infoReq.Length, ep);
                var cts = new CancellationTokenSource(3000);
                var res = await udp.ReceiveAsync(cts.Token);
                return res.Buffer.Length > 5;
            }

            if (plugin is IRestPlayersPlugin rest)
            {
                // A REST response (even empty list) means the game is up
                var players = await rest.GetPlayersAsync(server);
                return rest.LastRestApiError == null;
            }

            if (plugin.HasRcon && server.RconPort > 0 && !string.IsNullOrEmpty(server.RconPassword))
            {
                // Try a lightweight TCP connect to RCON port
                using var tcp = new TcpClient();
                var cts = new CancellationTokenSource(3000);
                await tcp.ConnectAsync("127.0.0.1", server.RconPort, cts.Token);
                return tcp.Connected;
            }

            // No query method available — assume alive
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task TriggerFreezeActionAsync(GameServer server)
    {
        var msg = $"⚠ **Server freeze detected** — **{server.DisplayName}** is not responding to health checks.";

        if (_config.HealthCheckAction == HealthCheckAction.Restart)
        {
            msg += " Restarting automatically.";
            await _notifications.NotifyAsync($"🔄 Health Check — {server.DisplayName}", msg, "#D29922");
            await _manager.StopAsync(server);
            await Task.Delay(3000);
            await _manager.StartAsync(server);
        }
        else
        {
            await _notifications.NotifyAsync($"🚨 Health Check — {server.DisplayName}", msg, "#F85149");
        }

        // Always log to server console
        _manager.InjectLogLine(server.Id,
            $"[HealthCheck] Server freeze detected — action: {_config.HealthCheckAction}",
            ConsoleMessageType.Warning);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool CanHealthCheck(GameServer server, IGamePlugin plugin)
        => plugin is IA2SQueryPlugin
        || plugin is IRestPlayersPlugin
        || (plugin.HasRcon && server.RconPort > 0 && !string.IsNullOrEmpty(server.RconPassword));
}

public enum HealthCheckAction { Notify, Restart }
