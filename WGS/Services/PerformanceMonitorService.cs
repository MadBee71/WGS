using System.Collections.ObjectModel;
using System.Diagnostics;

namespace WGS.Services;

public record PerformanceSnapshot(DateTime Time, double CpuPercent, long MemoryMb);

public class ProcessMonitor
{
    private readonly int _pid;
    private Process? _proc;
    private TimeSpan _lastCpu;
    private DateTime _lastCpuTime;

    public ObservableCollection<PerformanceSnapshot> History { get; } = [];

    public double CurrentCpu { get; private set; }
    public long   CurrentMemMb { get; private set; }

    public ProcessMonitor(int pid)
    {
        _pid     = pid;
        _proc    = Process.GetProcessById(pid);
        _lastCpu = _proc.TotalProcessorTime;
        _lastCpuTime = DateTime.UtcNow;
    }

    public void Sample()
    {
        try
        {
            _proc ??= Process.GetProcessById(_pid);
            _proc.Refresh();

            var now     = DateTime.UtcNow;
            var cpuNow  = _proc.TotalProcessorTime;
            var elapsed = (now - _lastCpuTime).TotalSeconds;

            if (elapsed > 0)
            {
                CurrentCpu = (cpuNow - _lastCpu).TotalSeconds / elapsed / Environment.ProcessorCount * 100.0;
                CurrentCpu = Math.Clamp(CurrentCpu, 0, 100);
            }

            _lastCpu     = cpuNow;
            _lastCpuTime = now;

            CurrentMemMb = _proc.WorkingSet64 / (1024 * 1024);

            var snap = new PerformanceSnapshot(DateTime.Now, CurrentCpu, CurrentMemMb);
            History.Add(snap);
            if (History.Count > 120) History.RemoveAt(0); // 2 min at 1s interval
        }
        catch { }
    }
}

public class PerformanceMonitorService : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private readonly Dictionary<string, ProcessMonitor> _monitors = new();

    public PerformanceMonitorService()
    {
        _timer = new System.Timers.Timer(2000);
        _timer.Elapsed += (_, _) => SampleAll();
        _timer.Start();
    }

    public void Track(string serverId, int pid)
    {
        try { _monitors[serverId] = new ProcessMonitor(pid); }
        catch { }
    }

    public void Untrack(string serverId) => _monitors.Remove(serverId);

    public ProcessMonitor? Get(string serverId)
        => _monitors.TryGetValue(serverId, out var m) ? m : null;

    private void SampleAll()
    {
        foreach (var m in _monitors.Values) m.Sample();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
