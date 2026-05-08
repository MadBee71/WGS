using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace WGS.Services;

public class SteamCmdService
{
    private static readonly HttpClient _http = new();

    private readonly string _steamCmdDir;
    private readonly string _steamCmdExe;
    private Process? _currentProcess;

    public event Action<string>? OutputReceived;
    public event Action<int>? ProgressChanged;
    public event Action? Completed;

    public SteamCmdService(ConfigService config)
    {
        _steamCmdDir = Path.Combine(config.AppDataPath, "steamcmd");
        _steamCmdExe = Path.Combine(_steamCmdDir, "steamcmd.exe");
    }

    public bool IsInstalled => File.Exists(_steamCmdExe);

    public async Task DownloadSteamCmdAsync()
    {
        try
        {
            Directory.CreateDirectory(_steamCmdDir);
            var zipPath = Path.Combine(_steamCmdDir, "steamcmd.zip");
            OutputReceived?.Invoke("[WGS] Downloading SteamCMD...");

            var bytes = await _http.GetByteArrayAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");
            await File.WriteAllBytesAsync(zipPath, bytes);

            OutputReceived?.Invoke("[WGS] Extracting SteamCMD...");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, _steamCmdDir, overwriteFiles: true);
            File.Delete(zipPath);
            OutputReceived?.Invoke("[WGS] SteamCMD installed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            OutputReceived?.Invoke("[ERR] SteamCMD download failed: " + ex.Message);
            throw new InvalidOperationException("SteamCMD download failed", ex);
        }
    }

    public async Task InstallOrUpdateAsync(int appId, string installPath, string? login = null, string? password = null)
    {
        if (!IsInstalled) await DownloadSteamCmdAsync();

        try
        {
            Directory.CreateDirectory(installPath);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Cannot create install directory: " + ex.Message, ex);
        }

        var loginArg = (login != null && password != null)
            ? $"+login {login} {password}"
            : "+login anonymous";

        var args = $"+force_install_dir \"{installPath}\" {loginArg} +app_update {appId} validate +quit";
        await RunAsync(args, throwOnSteamError: true);
        CleanupCache();
    }

    private void CleanupCache()
    {
        foreach (var dir in new[] { "appcache", "depotcache" })
        {
            var path = Path.Combine(_steamCmdDir, dir);
            if (Directory.Exists(path))
                try { Directory.Delete(path, recursive: true); } catch { }
        }
    }

    public async Task RunAsync(string arguments, bool throwOnSteamError = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = _steamCmdExe,
            Arguments              = arguments,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding  = System.Text.Encoding.UTF8,
        };

        string? steamError = null;
        _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _currentProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            ParseAndForward(e.Data);

            if (throwOnSteamError && e.Data.StartsWith("ERROR!", StringComparison.OrdinalIgnoreCase))
                steamError = e.Data;
        };
        _currentProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) OutputReceived?.Invoke("[ERR] " + e.Data); };

        try
        {
            _currentProcess.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
        {
            throw new InvalidOperationException(
                $"Failed to start SteamCMD process ({_steamCmdExe}): {ex.Message}", ex);
        }

        _currentProcess.BeginOutputReadLine();
        _currentProcess.BeginErrorReadLine();
        await _currentProcess.WaitForExitAsync();

        var exitCode = _currentProcess.ExitCode;
        Completed?.Invoke();

        if (steamError != null)
            throw new InvalidOperationException(steamError);

        if (exitCode != 0 && steamError == null)
            OutputReceived?.Invoke($"[WGS] SteamCMD exited with code {exitCode}");
    }

    private void ParseAndForward(string line)
    {
        OutputReceived?.Invoke(line);
        if (line.Contains("Update state (0x") && line.Contains("downloading") && line.Contains("%"))
        {
            var pct = ExtractPercent(line);
            if (pct >= 0) ProgressChanged?.Invoke(pct);
        }
    }

    private static int ExtractPercent(string line)
    {
        var idx = line.IndexOf('%');
        if (idx < 0) return -1;
        var start = idx - 1;
        while (start > 0 && (char.IsDigit(line[start - 1]) || line[start - 1] == '.')) start--;
        return double.TryParse(line[start..idx], out var d) ? (int)d : -1;
    }

    public void Cancel() => _currentProcess?.Kill(entireProcessTree: true);
}
