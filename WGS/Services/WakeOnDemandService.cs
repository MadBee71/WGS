using System.Net;
using System.Net.Sockets;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Listens on a server's port while it is stopped. When any TCP connection arrives
/// (a player trying to connect) the listener stops and the server is started.
/// </summary>
public sealed class WakeOnDemandService : IDisposable
{
    private readonly ServerManagerService _manager;
    private readonly Dictionary<string, CancellationTokenSource> _watchers = new();
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

    /// <summary>Stop the listener (called when server is started normally or deleted).</summary>
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

    private async Task ListenAsync(GameServer server, CancellationToken ct)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Any, server.ServerPort);
            listener.Start();

            // Accept exactly one connection, then start the server
            using var reg = ct.Register(() => listener.Stop());
            var client = await listener.AcceptTcpClientAsync(ct);
            client.Dispose();
        }
        catch (OperationCanceledException) { return; }
        catch { return; }
        finally
        {
            try { listener?.Stop(); } catch { }
        }

        // Remove watcher entry before starting
        lock (_lock) { _watchers.Remove(server.Id); }

        try { await _manager.StartAsync(server); }
        catch { }
    }

    public void Dispose()
    {
        List<CancellationTokenSource> all;
        lock (_lock)
        {
            all = [.. _watchers.Values];
            _watchers.Clear();
        }
        foreach (var cts in all) { cts.Cancel(); cts.Dispose(); }
    }
}
