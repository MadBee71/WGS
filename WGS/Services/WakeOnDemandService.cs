using System.Net;
using System.Net.Sockets;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Two features:
/// 1. Wake on demand — listens on the server port while stopped; starts the server when a player connects.
/// 2. Shut down when empty — polls player count while running; stops the server after idle timeout.
/// </summary>
public sealed class WakeOnDemandService : IDisposable
{
    private readonly ServerManagerService _manager;
    private readonly BackupService _backup;
    private readonly NotificationService _notifications;
    private readonly Dictionary<string, CancellationTokenSource> _watchers  = new();
    private readonly Dictionary<string, CancellationTokenSource> _idleWatchers = new();
    private readonly object _lock = new();

    /// <summary>Raised after an idle-shutdown backup completes, so an open Backups tab can refresh.</summary>
    public event Action<string>? ServerBackedUp;

    public WakeOnDemandService(ServerManagerService manager, BackupService backup, NotificationService notifications)
    {
        _manager = manager;
        _backup  = backup;
        _notifications = notifications;
    }

    /// <summary>Start wake-on-demand listener for a stopped server.</summary>
    public void Arm(GameServer server)
    {
        if (!server.WakeOnDemand) return;

        lock (_lock)
        {
            if (_watchers.ContainsKey(server.Id)) return;
            var cts = new CancellationTokenSource();
            _watchers[server.Id] = cts;
            _ = ListenAsync(server, cts.Token);
        }
    }

    /// <summary>Start idle-shutdown watcher for a running server.</summary>
    public void ArmIdleShutdown(GameServer server)
    {
        if (!server.ShutDownWhenEmpty) return;

        lock (_lock)
        {
            if (_idleWatchers.ContainsKey(server.Id)) return;
            var cts = new CancellationTokenSource();
            _idleWatchers[server.Id] = cts;
            _ = IdleWatchAsync(server, cts.Token);
        }
    }

    /// <summary>Stop the wake-on-demand listener (called when server starts or is deleted).</summary>
    public void Disarm(string serverId)
    {
        lock (_lock)
        {
            if (!_watchers.TryGetValue(serverId, out var cts)) return;
            _watchers.Remove(serverId);
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <summary>Stop the idle-shutdown watcher (called when server stops or is deleted).</summary>
    public void DisarmIdleShutdown(string serverId)
    {
        lock (_lock)
        {
            if (!_idleWatchers.TryGetValue(serverId, out var cts)) return;
            _idleWatchers.Remove(serverId);
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task ListenAsync(GameServer server, CancellationToken ct)
    {
        // Race TCP and UDP on the same port number — OS allows both simultaneously since they are
        // separate protocols. Most games use UDP (Valheim, Rust, ARK, Palworld, DayZ…) but some
        // use TCP (Minecraft family, FiveM…). First signal from either protocol wins.
        using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linked = innerCts.Token;

        bool triggered = false;
        try
        {
            await Task.WhenAny(
                WaitForUdpAsync(server.ServerPort, linked),
                WaitForTcpAsync(server.ServerPort, linked));
            triggered = !ct.IsCancellationRequested;
        }
        catch (OperationCanceledException) { }
        finally
        {
            innerCts.Cancel(); // shut down whichever listener didn't fire
        }

        if (!triggered) return;

        // Remove watcher entry before starting
        lock (_lock) { _watchers.Remove(server.Id); }

        // The connection attempt that triggered this wake is consumed by our listener and gets
        // no reply, so the game client that sent it will report "can't connect" — the player needs
        // to retry once the server has actually finished starting. Make that expectation explicit.
        try
        {
            await _notifications.NotifyAsync(
                $"🔌 Waking up {server.DisplayName}",
                "An incoming connection triggered wake-on-demand. That first connection attempt won't succeed — wait for the server to finish starting, then connect again.",
                "#d29922");
        }
        catch { }

        try { await _manager.StartAsync(server); }
        catch { }
    }

    private static async Task WaitForUdpAsync(int port, CancellationToken ct)
    {
        using var udp = new UdpClient(port);
        using var reg = ct.Register(() => { try { udp.Close(); } catch { } });
        try { await udp.ReceiveAsync(ct); } catch { }
    }

    private static async Task WaitForTcpAsync(int port, CancellationToken ct)
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Any, port);
        listener.Start();
        using var reg = ct.Register(() => { try { listener.Stop(); } catch { } });
        try { await listener.AcceptTcpClientAsync(ct); }
        catch { }
        finally { try { listener.Stop(); } catch { } }
    }

    private async Task IdleWatchAsync(GameServer server, CancellationToken ct)
    {
        var idleStart = DateTime.UtcNow;
        var timeout   = TimeSpan.FromMinutes(Math.Max(1, server.ShutDownIdleMinutes));
        const int PollMs = 120_000; // check every 2 minutes

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollMs, ct);

                var inst = _manager.GetInstance(server.Id);
                if (inst == null) break; // server removed

                var players = inst.Server.CurrentPlayers;
                if (players > 0)
                {
                    idleStart = DateTime.UtcNow; // reset idle clock
                }
                else if (DateTime.UtcNow - idleStart >= timeout)
                {
                    lock (_lock) { _idleWatchers.Remove(server.Id); }
                    try { await _manager.StopAsync(server); } catch { }
                    if (server.BackupOnShutdown)
                    {
                        try { await _backup.CreateBackupAsync(server); ServerBackedUp?.Invoke(server.Id); } catch { }
                    }
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            lock (_lock)
            {
                if (_idleWatchers.TryGetValue(server.Id, out var stored) && stored.Token == ct)
                    _idleWatchers.Remove(server.Id);
            }
        }
    }

    public void Dispose()
    {
        List<CancellationTokenSource> all;
        lock (_lock)
        {
            all = [.. _watchers.Values, .. _idleWatchers.Values];
            _watchers.Clear();
            _idleWatchers.Clear();
        }
        foreach (var cts in all) { cts.Cancel(); cts.Dispose(); }
    }
}
