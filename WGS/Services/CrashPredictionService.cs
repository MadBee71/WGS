namespace WGS.Services;

public class CrashPrediction
{
    public string   ServerId   { get; set; } = "";
    public string   ServerName { get; set; } = "";
    public string   Reason     { get; set; } = "";
    public int      Severity   { get; set; } // 1=warning, 2=critical
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}

public class CrashPredictionService : IDisposable
{
    private readonly PerfHistoryService _perfHistory;
    private readonly Dictionary<string, DateTime> _lastFired = new();
    private CancellationTokenSource? _cts;
    private Task? _task;

    // Wired from MainViewModel after DI is fully built
    public Func<IEnumerable<(string id, string name)>>? GetRunningServers { get; set; }

    public event Action<string, CrashPrediction>? PredictionRaised;

    private static readonly TimeSpan Cooldown    = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan WindowSize  = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan SustainedCpuWindow = TimeSpan.FromMinutes(2);

    public CrashPredictionService(PerfHistoryService perfHistory)
    {
        _perfHistory = perfHistory;
        // Start() called via Initialize() from App.xaml.cs after DI is fully built
    }

    public void Initialize() => Start();

    private void Start()
    {
        _cts  = new CancellationTokenSource();
        _task = Task.Run(() => Loop(_cts.Token));
    }

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(30_000, ct); } catch (OperationCanceledException) { break; }
            Analyze();
        }
    }

    private void Analyze()
    {
        var servers = GetRunningServers?.Invoke() ?? [];
        foreach (var (id, name) in servers)
            AnalyzeServer(id, name);
    }

    private void AnalyzeServer(string serverId, string serverName)
    {
        var now       = DateTime.Now;        // capture once for consistency
        var windowStart = now - WindowSize;
        var sustainStart = now - SustainedCpuWindow;

        // Collect only the snapshots in the window in a single pass,
        // extracting everything we need without further enumeration.
        long firstMem = -1, lastMem = -1, maxMem = -1;
        bool prevAlwaysGrowing = true;
        long prevMemForLeak = -1;
        int  count = 0;
        int  recentCount = 0;
        bool allRecentHighCpu = true;

        foreach (var s in _perfHistory.Get(serverId))
        {
            if (s.Time < windowStart) continue;

            // RAM tracking (first pass)
            if (firstMem < 0) firstMem = s.MemMb;
            lastMem = s.MemMb;
            if (s.MemMb > maxMem) maxMem = s.MemMb;

            // Memory leak: monotonically growing
            if (prevMemForLeak >= 0 && s.MemMb <= prevMemForLeak)
                prevAlwaysGrowing = false;
            prevMemForLeak = s.MemMb;

            // Sustained CPU tracking
            if (s.Time >= sustainStart)
            {
                recentCount++;
                if (s.Cpu <= 95.0) allRecentHighCpu = false;
            }

            count++;
        }

        if (count < 3) return;

        // Check cooldown
        if (_lastFired.TryGetValue(serverId, out var last) && now - last < Cooldown) return;

        CrashPrediction? pred = null;

        // RAM growth >40% over window
        if (pred == null && firstMem > 0 && lastMem > firstMem)
        {
            double growth = (double)(lastMem - firstMem) / firstMem;
            if (growth > 0.40)
                pred = Make(serverId, serverName, "High RAM growth", 2, now);
        }

        // CPU above 95% for entire 2-minute window
        if (pred == null && recentCount > 0 && allRecentHighCpu)
            pred = Make(serverId, serverName, "Sustained CPU above 95%", 1, now);

        // Memory leak: RAM grew every sample across full window
        if (pred == null && count >= 5 && prevAlwaysGrowing)
            pred = Make(serverId, serverName, "Memory leak detected (RAM consistently growing)", 1, now);

        if (pred != null)
        {
            _lastFired[serverId] = now;
            PredictionRaised?.Invoke(serverId, pred);
        }
    }

    private static CrashPrediction Make(string id, string name, string reason, int severity, DateTime now) =>
        new() { ServerId = id, ServerName = name, Reason = reason, Severity = severity, DetectedAt = now };

    public void Dispose()
    {
        _cts?.Cancel();
        try { _task?.Wait(3000); } catch { }
    }
}
