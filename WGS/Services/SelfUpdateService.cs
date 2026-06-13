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
        var exeName = Path.GetFileName(exePath);

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

            // 3 — write bat: wait for this process to exit, swap exe, restart, clean up
            var pid = Environment.ProcessId;
            var bat = $@"@echo off
:wait
tasklist /fi ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait
)
move /y ""{newExePath}"" ""{exePath}""
start """" ""{exePath}""
del ""{tempZip}"" 2>nul
del ""%~f0""
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
            WindowStyle     = ProcessWindowStyle.Hidden,
        });

        System.Windows.Application.Current.Shutdown();
    }

    private static void Report(IProgress<(int, string)>? p, int pct, string msg)
        => p?.Report((pct, msg));
}
