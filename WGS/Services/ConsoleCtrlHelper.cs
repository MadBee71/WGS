using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace WGS.Services;

/// <summary>
/// Graceful stop for games that have no stdin/RCON/REST stop command but save and exit on Ctrl+C
/// (Valheim, RuneScape: Dragonwilds — verified against the real servers, also as a Windows service).
///
/// Ctrl+C can only be delivered by attaching to the target's console, and a console attachment is process-wide:
/// doing it inside WGS itself crashed the calling process about half of the time (tested 19.9.2026). So it is
/// done by a short-lived copy of this same executable started with <see cref="HelperArg"/>; WGS (or the service)
/// only waits for it. The helper refuses to send anything unless the console contains exactly the target and
/// itself (a Ctrl+C broadcast would otherwise hit every other process on that console), and it only ever signals —
/// it never kills anything. The caller falls back to the old hard kill if the game does not exit.
/// </summary>
public static class ConsoleCtrlHelper
{
    public const string HelperArg = "--ctrlc-helper";

    // Exit codes of the helper process.
    private const int Ok = 0, Failed = 1, BadArgs = 2, TargetGone = 3, AttachFailed = 4, NotIsolated = 5, SendFailed = 6;

    /// <summary>
    /// The "ignore Ctrl+C" flag of a process is inherited by every child. If WGS itself was started from a context
    /// that ignores Ctrl+C, the games it starts would silently ignore it too. Call right before starting a game.
    /// </summary>
    public static void EnsureCtrlCEnabled()
    {
        try { SetConsoleCtrlHandler(IntPtr.Zero, false); } catch { }
    }

    /// <summary>
    /// Asks the process to shut down with Ctrl+C and waits for it to exit. Never kills anything.
    /// Returns true if the process has exited; otherwise false with the reason in <paramref name="error"/>.
    /// </summary>
    public static async Task<(bool Exited, string? Error)> TryGracefulStopAsync(Process process, TimeSpan timeout)
    {
        try
        {
            if (process.HasExited) return (true, null);

            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)
                || Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                return (false, "no helper executable available");

            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add(HelperArg);
            psi.ArgumentList.Add(process.Id.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add(process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));

            using var helper = Process.Start(psi) ?? throw new InvalidOperationException("could not start the helper");
            try { await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (TimeoutException)
            {
                try { helper.Kill(); } catch { } // only the helper, never the game
                return (false, "helper timed out");
            }

            if (process.HasExited) return (true, null);
            if (helper.ExitCode != Ok)
                return (false, helper.ExitCode switch
                {
                    TargetGone   => "target process changed or is gone",
                    AttachFailed => "could not attach to the game's console",
                    NotIsolated  => "the game's console is shared with other processes",
                    SendFailed   => "Windows refused the Ctrl+C",
                    _            => $"helper failed (code {helper.ExitCode})"
                });

            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited) return (true, null);
                await Task.Delay(250);
            }
            return (process.HasExited, process.HasExited ? null : $"did not exit within {(int)timeout.TotalSeconds}s");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // e.g. the process already exited between the checks
            return (SafeExited(process), SafeExited(process) ? null : ex.Message);
        }
    }

    private static bool SafeExited(Process p)
    {
        try { return p.HasExited; } catch { return true; }
    }

    /// <summary>Entry point of the helper process: <c>--ctrlc-helper &lt;pid&gt; &lt;start time ticks (UTC)&gt;</c>. Returns the exit code.</summary>
    public static int RunHelper(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Length != 3
            || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
            || !long.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            return BadArgs;

        try
        {
            using var target = Process.GetProcessById(pid);
            if (target.HasExited || target.StartTime.ToUniversalTime().Ticks != ticks) return TargetGone;

            FreeConsole();
            if (!AttachConsole((uint)pid)) return AttachFailed;
            try
            {
                // This process must not die from the Ctrl+C it is about to send.
                SetConsoleCtrlHandler(IntPtr.Zero, true);

                var members = new uint[16];
                var count = GetConsoleProcessList(members, (uint)members.Length);
                if (count != 2 || !members.Take((int)count).Contains((uint)pid)
                    || !members.Take((int)count).Contains((uint)Environment.ProcessId))
                    return NotIsolated;

                if (target.HasExited || target.StartTime.ToUniversalTime().Ticks != ticks) return TargetGone;

                // CTRL_C_EVENT cannot be aimed at one process: it goes to everything on the console, which was
                // just verified to be only the target and this helper.
                if (!GenerateConsoleCtrlEvent(0, 0)) return SendFailed;
                Thread.Sleep(300); // let Windows deliver it before detaching
                return Ok;
            }
            finally { FreeConsole(); }
        }
        catch { return Failed; }
    }

    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint GetConsoleProcessList(uint[] ids, uint count);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GenerateConsoleCtrlEvent(uint signal, uint group);
}
