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
    private readonly Dictionary<string, CancellationTokenSource> _watchers  = new();
    private readonly Dictionary<string, CancellationTokenSource> _idleWatchers = new();
    private readonly object _lock = new();

    public WakeOnDemandService(ServerManagerService manager)
    {
        _manager = manager;
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
        // Game traffic on the main server port is UDP for almost every supported game
        // (Valheim, Rust, ARK, Palworld, 7 Days to Die, DayZ...) — a TCP listener here would
        // never see a real client's connection attempt. Wake on any inbound datagram instead.
        UdpClient? listener = null;
        try
        {
            listener = new UdpClient(server.ServerPort);
            using var reg = ct.Register(() => listener.Dispose());
            await listener.ReceiveAsync(ct);
        }
        catch (OperationCanceledException) { return; }
        catch (ObjectDisposedException) { return; }
        catch { return; }
        finally
        {
            try { listener?.Dispose(); } catch { }
        }

        // Remove watcher entry before starting
        lock (_lock) { _watchers.Remove(server.Id); }

        try { await _manager.StartAsync(server); }
        catch { }
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
