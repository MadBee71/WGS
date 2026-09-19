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
    private readonly Dictionary<string, DateTime> _idleShutdownTimes = new();
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
        if (!server.WakeOnDemand || !server.WakeOnDemandPortTrigger) return;

        lock (_lock)
        {
            if (_watchers.ContainsKey(server.Id)) return;

            // The armed listener binds server.ServerPort BEFORE the server is actually running,
            // but FirewallService.AddRules is normally only called by StartAsync — so a forwarded
            // port that reaches this machine gets dropped by Windows Firewall's default inbound
            // block before it ever reaches this listener (DatBrokeBoi, Discord forum, 19.9.2026:
            // "WGS can't scan that port... after setting up Wake on demand"). Idempotent — AddRules
            // removes any existing rule of the same name before recreating it, so calling it again
            // when the server actually starts (StartAsync's own AddRules call) is harmless.
            if (server.FirewallAutoManage) FirewallService.AddRules(server);

            var cts = new CancellationTokenSource();
            _watchers[server.Id] = cts;
            // After an idle shutdown, wait 5 minutes before listening again so that
            // game-client reconnect retries don't immediately wake the server back up.
            TimeSpan delay = TimeSpan.Zero;
            if (_idleShutdownTimes.TryGetValue(server.Id, out var t))
            {
                _idleShutdownTimes.Remove(server.Id);
                var elapsed = DateTime.UtcNow - t;
                var cooldown = TimeSpan.FromMinutes(5);
                if (elapsed < cooldown) delay = cooldown - elapsed;
            }
            _ = ListenAsync(server, cts.Token, delay);
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

    private async Task ListenAsync(GameServer server, CancellationToken ct, TimeSpan delay = default)
    {
        // Race TCP and UDP on the same port number — OS allows both simultaneously since they are
        // separate protocols. Most games use UDP (Valheim, Rust, ARK, Palworld, DayZ…) but some
        // use TCP (Minecraft family, FiveM…). First signal from either protocol wins.
        using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linked = innerCts.Token;

        if (delay > TimeSpan.Zero)
        {
            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { return; }
        }

        bool triggered = false;
        try
        {
            await Task.WhenAny(
                WaitForUdpAsync(server.Id, server.ServerPort, linked),
                WaitForTcpAsync(server.Id, server.ServerPort, linked));
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

    // Binding can fail right after Arm() runs — most commonly because the OS hasn't finished
    // releasing the port yet from the process that just stopped (TCP TIME_WAIT, or a UDP socket
    // still tearing down). The old code let that SocketException propagate out of
    // WaitForUdpAsync/WaitForTcpAsync, which Task.WhenAny in ListenAsync then treated as if a
    // real connection had arrived (it only checks whether ct was cancelled, not why the awaited
    // task completed) — so a transient bind failure immediately, wrongly, "woke" the server, and
    // if the retry-start then hit the same still-held port it could fail again and land the
    // server in an Error/dead-looking state seconds after arming (DatBrokeBoi, Discord forum,
    // 20.9.2026: "goes blue then grey like it died... trying to connect doesn't wake it").
    // Retrying the bind itself — instead of ever letting a bind failure look like a trigger —
    // fixes this at the source. Pre-existing bug, not introduced by the firewall-rule fix above.
    private const int BindRetryDelayMs = 1000;
    private const int BindLogWarningAfterAttempts = 15; // ~15s of retries — worth surfacing by then

    private async Task<UdpClient?> BindUdpWithRetryAsync(string serverId, int port, CancellationToken ct)
    {
        for (var attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            try { return new UdpClient(port); }
            catch (SocketException)
            {
                if (attempt == BindLogWarningAfterAttempts)
                    _manager.InjectLogLine(serverId,
                        $"[Wake on Demand] Still waiting for UDP port {port} to become free — retrying.",
                        ConsoleMessageType.Warning);
                try { await Task.Delay(BindRetryDelayMs, ct); } catch (OperationCanceledException) { return null; }
            }
        }
        return null;
    }

    private async Task<TcpListener?> BindTcpWithRetryAsync(string serverId, int port, CancellationToken ct)
    {
        for (var attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            var candidate = new TcpListener(IPAddress.Any, port);
            try { candidate.Start(); return candidate; }
            catch (SocketException)
            {
                if (attempt == BindLogWarningAfterAttempts)
                    _manager.InjectLogLine(serverId,
                        $"[Wake on Demand] Still waiting for TCP port {port} to become free — retrying.",
                        ConsoleMessageType.Warning);
                try { await Task.Delay(BindRetryDelayMs, ct); } catch (OperationCanceledException) { return null; }
            }
        }
        return null;
    }

    private async Task WaitForUdpAsync(string serverId, int port, CancellationToken ct)
    {
        using var udp = await BindUdpWithRetryAsync(serverId, port, ct);
        if (udp == null) return; // cancelled while retrying — never treat that as a trigger
        using var reg = ct.Register(() => { try { udp.Close(); } catch { } });
        try { await udp.ReceiveAsync(ct); } catch { }
    }

    private async Task WaitForTcpAsync(string serverId, int port, CancellationToken ct)
    {
        var listener = await BindTcpWithRetryAsync(serverId, port, ct);
        if (listener == null) return; // cancelled while retrying — never treat that as a trigger
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
                    lock (_lock)
                    {
                        _idleWatchers.Remove(server.Id);
                        _idleShutdownTimes[server.Id] = DateTime.UtcNow;
                    }
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
