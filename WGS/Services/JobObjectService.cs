using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WGS.Services;

/// <summary>
/// Applies a RAM limit to a process using Windows Job Objects.
/// The limit is a soft commit limit — the process is killed by the OS if it exceeds it.
/// </summary>
public static class JobObjectService
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateJobObject(nint attr, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(nint job, int cls, ref JobObjectExtendedLimitInfo info, int len);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint job, nint process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInfo
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinWorkingSetSize, MaxWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInfo
    {
        public JobObjectBasicLimitInfo BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    private const int JobObjectExtendedLimitInfoClass = 9;
    private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100;

    /// <summary>
    /// Applies a RAM limit (MB) to the process. Returns the job handle (must be kept alive).
    /// Returns IntPtr.Zero if the limit is 0 or the call fails.
    /// </summary>
    public static nint ApplyRamLimit(Process proc, long maxRamMb)
    {
        if (maxRamMb <= 0) return nint.Zero;
        try
        {
            var job = CreateJobObject(nint.Zero, null);
            if (job == nint.Zero) return nint.Zero;

            var info = new JobObjectExtendedLimitInfo
            {
                BasicLimitInformation = new JobObjectBasicLimitInfo
                {
                    LimitFlags         = JOB_OBJECT_LIMIT_PROCESS_MEMORY,
                },
                ProcessMemoryLimit = (nuint)(maxRamMb * 1024 * 1024),
            };

            if (!SetInformationJobObject(job, JobObjectExtendedLimitInfoClass, ref info, Marshal.SizeOf(info)))
            {
                CloseHandle(job);
                return nint.Zero;
            }

            if (!AssignProcessToJobObject(job, proc.Handle))
            {
                CloseHandle(job);
                return nint.Zero;
            }

            return job;
        }
        catch { return nint.Zero; }
    }

    public static void ReleaseJob(nint jobHandle)
    {
        if (jobHandle != nint.Zero)
            try { CloseHandle(jobHandle); } catch { }
    }
}
