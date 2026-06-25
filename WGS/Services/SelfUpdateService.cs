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
            // 1 — download zip with progress reporting
            Report(progress, 5, "Downloading update...");
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(5);
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("WGS", UpdateCheckerService.GetCurrentVersion()));

            using var response = await http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var buffer     = new byte[81920];
            var downloaded = 0L;

            await using var src  = await response.Content.ReadAsStreamAsync();
            await using var dest = File.Create(tempZip);

            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                if (totalBytes > 0)
                {
                    // Scale download progress to 5–55%
                    var pct = 5 + (int)(50.0 * downloaded / totalBytes);
                    Report(progress, pct, $"Downloading... {downloaded / 1_048_576.0:F1} / {totalBytes / 1_048_576.0:F1} MB");
                }
            }

            Report(progress, 60, "Extracting...");

            // 2 — extract exe from zip (verified size > 5 MB)
            using var zip = ZipFile.OpenRead(tempZip);
            var entry = zip.Entries.FirstOrDefault(e =>
                e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                throw new InvalidOperationException("No .exe found inside the release zip.");

            if (entry.Length < 5 * 1_048_576)
                throw new InvalidOperationException($"Downloaded exe is too small ({entry.Length / 1024} KB) — possible corrupt download.");

            if (File.Exists(newExePath)) File.Delete(newExePath);
            entry.ExtractToFile(newExePath);
            Report(progress, 80, "Preparing update script...");

            // 3 — write bat:
            //   a) wait for old WGS (this process) to exit
            //   b) swap exe with retries (AV may lock it briefly)
            //   c) start new WGS
            //   d) wait until new WGS process is running (up to 30 s)
            //   e) exit — bat stays alive until new WGS is confirmed up
            //
            // Leftover files cleaned up by CleanupLeftovers() on next startup.
            var pid     = Environment.ProcessId;
            var exeName = Path.GetFileNameWithoutExtension(exePath); // e.g. "WindowsGameServer"
            var bat     = $@"@echo off
setlocal

rem ── Step 1: wait for old WGS (PID {pid}) to exit ──────────────────────────
:waitOld
tasklist /fi ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitOld
)

rem ── Step 2: swap exe (up to 30 retries) ───────────────────────────────────
set retries=0
:moveretry
move /y ""{newExePath}"" ""{exePath}"" >nul 2>nul
if exist ""{newExePath}"" (
    set /a retries+=1
    if %retries% lss 30 (
        timeout /t 1 /nobreak >nul
        goto moveretry
    )
    echo ERROR: Could not replace exe after 30 attempts.
    pause
    goto cleanup
)

rem ── Step 3: start new WGS ─────────────────────────────────────────────────
start """" ""{exePath}""

rem ── Step 4: wait until new WGS process is running (up to 30 s) ───────────
set waitNew=0
:waitNew
tasklist /fi ""IMAGENAME eq {exeName}.exe"" 2>nul | find /i ""{exeName}.exe"" >nul
if not errorlevel 1 goto done
set /a waitNew+=1
if %waitNew% lss 30 (
    timeout /t 1 /nobreak >nul
    goto waitNew
)
echo WARNING: New WGS did not appear within 30 seconds.
pause

:done
rem ── Step 5: cleanup ───────────────────────────────────────────────────────
:cleanup
del ""{tempZip}"" 2>nul
exit /b
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

    /// <summary>
    /// Launches the update bat and shuts down WGS — but ONLY after verifying the bat
    /// process is alive. If the bat fails to start, WGS keeps running and this returns false.
    /// </summary>
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
                WindowStyle     = ProcessWindowStyle.Minimized,
            });
        }
        catch { }

        // Confirm bat is alive within 2 seconds before shutting down WGS
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (proc != null && !proc.HasExited)
                break;
            System.Threading.Thread.Sleep(100);
        }

        if (proc == null || proc.HasExited)
        {
            // bat never started or died instantly — abort, WGS stays running
            try { if (File.Exists(batPath)) File.Delete(batPath); } catch { }
            return false;
        }

        System.Windows.Application.Current.Shutdown();
        return true;
    }

    /// <summary>Removes leftover update files from a previous run. Call once on startup.
    /// Returns true if "_wgs_new.exe" was left behind unswapped — the move failed every
    /// retry, so WGS is still on the OLD version despite appearing to update.</summary>
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
