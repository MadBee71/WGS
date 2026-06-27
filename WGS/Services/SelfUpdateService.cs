using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;

namespace WGS.Services;

public static class SelfUpdateService
{
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
            Report(progress, 5, "Downloading update...");
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
            http.Timeout = TimeSpan.FromMinutes(5);
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("WGS", UpdateCheckerService.GetCurrentVersion()));

            var bytes = await http.GetByteArrayAsync(zipUrl);
            await File.WriteAllBytesAsync(tempZip, bytes);
            Report(progress, 60, "Extracting...");

            using var zip = ZipFile.OpenRead(tempZip);
            var entry = zip.Entries.FirstOrDefault(e =>
                e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                throw new InvalidOperationException("No .exe found inside the release zip.");

            if (File.Exists(newExePath)) File.Delete(newExePath);
            entry.ExtractToFile(newExePath);
            Report(progress, 80, "Preparing update script...");

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
        catch (Exception ex)
        {
            Report(progress, 0, $"Error: {ex.Message}");
            try { if (File.Exists(tempZip))    File.Delete(tempZip); }    catch { }
            try { if (File.Exists(newExePath)) File.Delete(newExePath); } catch { }
            try { if (File.Exists(batPath))    File.Delete(batPath); }    catch { }
            return false;
        }
    }

    public static bool ApplyAndRestart()
    {
        var exeDir  = Path.GetDirectoryName(
            Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "WindowsGameServer.exe"))!;
        var batPath = Path.Combine(exeDir, "_wgs_update.bat");

        Process? proc = null;
        try
        {
            proc = Process.Start(new ProcessStartInfo
            {
                FileName        = batPath,
                UseShellExecute = true,
                WindowStyle     = ProcessWindowStyle.Hidden,
            });
        }
        catch { }

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (proc != null && !proc.HasExited) break;
            System.Threading.Thread.Sleep(100);
        }

        if (proc == null || proc.HasExited)
        {
            try { if (File.Exists(batPath)) File.Delete(batPath); } catch { }
            return false;
        }

        System.Windows.Application.Current.Shutdown();
        return true;
    }

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
