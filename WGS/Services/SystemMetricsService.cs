using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WGS.Services;

public record DriveStats(string Name, double UsedGb, double TotalGb)
{
    public double UsedPercent => TotalGb > 0 ? UsedGb / TotalGb * 100.0 : 0;
}

public class SystemMetricsService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private PerformanceCounter? _cpuCounter;
    private readonly System.Timers.Timer _timer;

    public float CpuPercent { get; private set; }
    public long RamUsedMb { get; private set; }
    public long RamTotalMb { get; private set; }
    public IReadOnlyList<DriveStats> Drives { get; private set; } = [];

    public event Action? MetricsUpdated;

    public SystemMetricsService()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
            _cpuCounter.NextValue(); // first call always returns 0 — prime it
        }
        catch
        {
            _cpuCounter = null;
        }

        _timer = new System.Timers.Timer(2000);
        _timer.Elapsed += (_, _) => Refresh();
        _timer.AutoReset = true;
        _timer.Start();

        Refresh();
    }

    private void Refresh()
    {
        try
        {
            if (_cpuCounter != null)
                CpuPercent = Math.Clamp(_cpuCounter.NextValue(), 0f, 100f);
        }
        catch { }

        try
        {
            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref mem))
            {
                RamTotalMb = (long)(mem.ullTotalPhys / 1024 / 1024);
                var availMb = (long)(mem.ullAvailPhys / 1024 / 1024);
                RamUsedMb  = RamTotalMb - availMb;
            }
        }
        catch { }

        try
        {
            Drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => new DriveStats(
                    d.Name,
                    Math.Round((d.TotalSize - d.AvailableFreeSpace) / 1_073_741_824.0, 1),
                    Math.Round(d.TotalSize / 1_073_741_824.0, 1)))
                .ToList();
        }
        catch { }

        MetricsUpdated?.Invoke();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _cpuCounter?.Dispose();
    }
}
