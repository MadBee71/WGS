using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using WGS.Models;

namespace WGS.Services;

public class NetworkSample
{
    public DateTime Time     { get; set; }
    public double   BytesIn  { get; set; }  // bytes/sec
    public double   BytesOut { get; set; }  // bytes/sec
}

public class NetworkMonitorService : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private long     _lastBytesIn;
    private long     _lastBytesOut;
    private DateTime _lastSample = DateTime.Now;

    // serverId → stats
    private readonly ConcurrentDictionary<string, ServerNetworkStats> _serverStats = new();

    // serverId → PID (päivitetään ulkoa Register-kutsuilla)
    private readonly ConcurrentDictionary<string, int> _serverPids = new();

    public double CurrentBytesInPerSec  { get; private set; }
    public double CurrentBytesOutPerSec { get; private set; }

    public string InText  => FormatSpeed(CurrentBytesInPerSec);
    public string OutText => FormatSpeed(CurrentBytesOutPerSec);

    public event Action?           Updated;
    public event Action<string>?   ServerStatsUpdated; // serverId

    public NetworkMonitorService()
    {
        (_lastBytesIn, _lastBytesOut) = GetTotalBytes();
        _timer = new System.Timers.Timer(2000);
        _timer.Elapsed += (_, _) => Sample();
        _timer.AutoReset = true;
        _timer.Start();
    }

    // ── Rekisteröinti ─────────────────────────────────────────────────────────

    /// <summary>Rekisteröi palvelimen PID seurantaan kun prosessi käynnistyy.</summary>
    public void RegisterServer(string serverId, int pid)
    {
        _serverPids[serverId] = pid;
        _serverStats.TryAdd(serverId, new ServerNetworkStats { ServerId = serverId });
    }

    /// <summary>Poistaa palvelimen seurannasta kun se pysäytetään.</summary>
    public void UnregisterServer(string serverId)
    {
        _serverPids.TryRemove(serverId, out _);
        if (_serverStats.TryGetValue(serverId, out var stats))
        {
            stats.ConnectionCount = 0;
            stats.BytesInPerSec   = 0;
            stats.BytesOutPerSec  = 0;
        }
        ServerStatsUpdated?.Invoke(serverId);
    }

    public ServerNetworkStats? GetServerStats(string serverId)
        => _serverStats.TryGetValue(serverId, out var s) ? s : null;

    // ── Näytteistys ───────────────────────────────────────────────────────────

    private void Sample()
    {
        try
        {
            var (bytesIn, bytesOut) = GetTotalBytes();
            var now     = DateTime.Now;
            var elapsed = (now - _lastSample).TotalSeconds;
            if (elapsed <= 0) return;

            double totalIn  = Math.Max(0, (bytesIn  - _lastBytesIn)  / elapsed);
            double totalOut = Math.Max(0, (bytesOut - _lastBytesOut) / elapsed);

            CurrentBytesInPerSec  = totalIn;
            CurrentBytesOutPerSec = totalOut;

            _lastBytesIn  = bytesIn;
            _lastBytesOut = bytesOut;
            _lastSample   = now;

            Updated?.Invoke();

            // Per-prosessi: vain jos on rekisteröityjä palvelimia
            if (!_serverPids.IsEmpty)
                UpdateServerStats(totalIn, totalOut);
        }
        catch (Exception ex) { Debug.WriteLine($"[NetworkMonitor] Sample() error: {ex.Message}"); } // #6
    }

    private void UpdateServerStats(double totalIn, double totalOut)
    {
        try
        {
            // Hae kaikki TCP-yhteydet kerran — yksi P/Invoke-kutsu riittää
            var connsByPid = ProcessNetworkTracker.GetConnectionCountsByPid();

            // Laske kaikki aktiiviset yhteydet referenssiksi
            int totalConns = connsByPid.Values.Sum();

            foreach (var (serverId, pid) in _serverPids)
            {
                if (!_serverStats.TryGetValue(serverId, out var stats))
                {
                    stats = new ServerNetworkStats { ServerId = serverId };
                    _serverStats[serverId] = stats;
                }

                // Onko prosessi vielä elossa?
                bool alive = false;
                try { alive = !Process.GetProcessById(pid).HasExited; }
                catch { }

                if (!alive)
                {
                    // Prosessi ei enää käynnissä — nolla-arvot
                    stats.ConnectionCount = 0;
                    stats.BytesInPerSec   = 0;
                    stats.BytesOutPerSec  = 0;
                    stats.PushHistory(0, 0);
                    ServerStatsUpdated?.Invoke(serverId);
                    continue;
                }

                connsByPid.TryGetValue(pid, out int serverConns);
                stats.ConnectionCount = serverConns;

                // Kaistaosuus = tämän palvelimen yhteydet / kaikki koneen yhteydet
                // Painotettu minimikynnyksen yläpuolella olevan liikenteen mukaan
                double share = totalConns > 0 && serverConns > 0
                    ? (double)serverConns / totalConns
                    : 0.0;

                stats.BytesInPerSec  = totalIn  * share;
                stats.BytesOutPerSec = totalOut * share;
                stats.PushHistory(stats.BytesInPerSec, stats.BytesOutPerSec);

                ServerStatsUpdated?.Invoke(serverId);
            }
        }
        catch { }
    }

    // ── Konekohtainen kaista ──────────────────────────────────────────────────

    private static (long bytesIn, long bytesOut) GetTotalBytes()
    {
        long bytesIn = 0, bytesOut = 0;
        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (iface.OperationalStatus    != OperationalStatus.Up)         continue;
            var stats = iface.GetIPv4Statistics();
            bytesIn  += stats.BytesReceived;
            bytesOut += stats.BytesSent;
        }
        return (bytesIn, bytesOut);
    }

    public static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec < 1024)            return $"{bytesPerSec:F0} B/s";
        if (bytesPerSec < 1024 * 1024)     return $"{bytesPerSec / 1024:F1} KB/s";
        return $"{bytesPerSec / (1024 * 1024):F1} MB/s";
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
