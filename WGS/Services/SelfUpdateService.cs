using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;

namespace WGS.Services;

public static class SelfUpdateService
{
    /// <summary>
    /// Downloads the zip, extracts the exe, writes an update.bat that swaps files and
    /// restarts WGS after this process exits. Returns false if anything fails.
    /// </summary>
    public static async Task<bool> DownloadAndPrepareAsync(
        string zipUrl,
        IProgress<(int pct, string msg)>? progress = null)
    {
        var exePath = Environment.ProcessPath
                      ?? Path.Combine(AppContext.BaseDirectory, "WindowsGameServer.exe");
        var exeDir  = Path.GetDirectoryName(exePath)!;

        var tempZip    = Path.Combine(exeDir, "_wgs_update.zip");
        var newExePath = Path.Combine(exeDir, "_wgs_new.exe");
        var batPath    = Path.Combine(exeDir, "_wgs_update.bat");

        try
        {
            // 1 — download zip
            Report(progress, 5, "Downloading update...");
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(5);
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("WGS", UpdateCheckerService.GetCurrentVersion()));

            var bytes = await http.GetByteArrayAsync(zipUrl);
            await File.WriteAllBytesAsync(tempZip, bytes);
            Report(progress, 60, "Extracting...");

            // 2 — extract exe from zip
            using var zip = ZipFile.OpenRead(tempZip);
            var entry = zip.Entries.FirstOrDefault(e =>
                e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                throw new InvalidOperationException("No .exe found inside the release zip.");

            if (File.Exists(newExePath)) File.Delete(newExePath);
            entry.ExtractToFile(newExePath);
            Report(progress, 80, "Preparing update script...");

            // 3 — write bat: wait for this process to exit, swap exe, restart.
            // Leftover bat/zip files are cleaned up by CleanupLeftovers() on the next startup
            // instead of self-deleting here — a script that deletes itself right after a silent
            // exe swap is a classic AV heuristic trigger.
            var pid = Environment.ProcessId;
            var bat = $@"@echo off
:wait
tasklist /fi ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait
)
set retries=0
:moveretry
move /y ""{newExePath}"" ""{exePath}"" >nul 2>nul
if exist ""{newExePath}"" (
    set /a retries+=1
    if %retries% lss 30 (
        timeout /t 1 /nobreak >nul
        goto moveretry
    )
)
del ""{tempZip}"" 2>nul
start """" ""{exePath}""
";
            await File.WriteAllTextAsync(batPath, bat);
            Report(progress, 100, "Ready — restarting...");

            return true;
        }
        catch
        {
            // clean up partial downloads
            try { if (File.Exists(tempZip))    File.Delete(tempZip); }    catch { }
            try { if (File.Exists(newExePath)) File.Delete(newExePath); } catch { }
            try { if (File.Exists(batPath))    File.Delete(batPath); }    catch { }
            return false;
        }
    }

    /// <summary>Starts the update bat and exits this process.</summary>
    public static void ApplyAndRestart(IProgress<(int pct, string msg)>? progress = null)
    {
        var exeDir  = Path.GetDirectoryName(
            Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "WindowsGameServer.exe"))!;
        var batPath = Path.Combine(exeDir, "_wgs_update.bat");

        Process.Start(new ProcessStartInfo
        {
            FileName        = batPath,
            UseShellExecute = true,
            WindowStyle     = ProcessWindowStyle.Minimized,
        });

        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>Removes leftover update files from a previous run. Call once on startup.
    /// Returns true if a previous update attempt left "_wgs_new.exe" behind unswapped — that
    /// means the move failed every retry and WGS is still running the OLD version, even though
    /// the update appeared to "finish" and restart.</summary>
    public static bool CleanupLeftovers()
    {
        var exeDir = Path.GetDirectoryName(
            Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "WindowsGameServer.exe"))!;

        var updateFailed = File.Exists(Path.Combine(exeDir, "_wgs_new.exe"));

        foreach (var name in new[] { "_wgs_update.bat", "_wgs_update.zip", "_wgs_new.exe" })
        {
            try
            {
                var path = Path.Combine(exeDir, name);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        return updateFailed;
    }

    private static void Report(IProgress<(int, string)>? p, int pct, string msg)
        => p?.Report((pct, msg));
}
