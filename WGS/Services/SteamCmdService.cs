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

    /// <summary>(serverId, line) — serverId is "" for messages not tied to a specific server (e.g. downloading SteamCMD itself before any install starts).</summary>
    public event Action<string, string>? OutputReceived;
    public event Action<string, int>? ProgressChanged;

    public SteamCmdService(ConfigService config)
    {
        _steamCmdDir = Path.Combine(config.AppDataPath, "steamcmd");
        _steamCmdExe = Path.Combine(_steamCmdDir, "steamcmd.exe");
    }

    public bool IsInstalled => File.Exists(_steamCmdExe);

    /// <summary>SteamCMD downloads workshop items here: steamcmd/steamapps/workshop/content/{appId}/{itemId}</summary>
    public string WorkshopContentPath => Path.Combine(_steamCmdDir, "steamapps", "workshop", "content");

    public async Task DownloadSteamCmdAsync(string serverId = "")
    {
        try
        {
            Directory.CreateDirectory(_steamCmdDir);
            var zipPath = Path.Combine(_steamCmdDir, "steamcmd.zip");
            OutputReceived?.Invoke(serverId, "[WGS] Downloading SteamCMD...");

            var bytes = await _http.GetByteArrayAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");
            await File.WriteAllBytesAsync(zipPath, bytes);

            OutputReceived?.Invoke(serverId, "[WGS] Extracting SteamCMD...");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, _steamCmdDir, overwriteFiles: true);
            File.Delete(zipPath);
            OutputReceived?.Invoke(serverId, "[WGS] SteamCMD installed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            OutputReceived?.Invoke(serverId, "[ERR] SteamCMD download failed: " + ex.Message);
            throw new InvalidOperationException("SteamCMD download failed", ex);
        }
    }

    public async Task InstallOrUpdateAsync(string serverId, int appId, string installPath, string? login = null, string? password = null, string? branch = null, string? branchPassword = null)
    {
        if (!IsInstalled) await DownloadSteamCmdAsync(serverId);

        try
        {
            Directory.CreateDirectory(installPath);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Cannot create install directory: " + ex.Message, ex);
        }

        // Warn early if the target drive is critically low on space.
        try
        {
            var root = Path.GetPathRoot(installPath);
            if (root != null)
            {
                var drive = new DriveInfo(root);
                var freeGb = drive.AvailableFreeSpace / 1_073_741_824.0;
                if (freeGb < 10)
                    OutputReceived?.Invoke(serverId,
                        $"[WGS] ⚠ Low disk space on {root}: {freeGb:F1} GB free. Install may fail if the server files don't fit.");
            }
        }
        catch { }

        var branchArg = string.IsNullOrEmpty(branch) ? "" : $" -beta {branch}";
        if (!string.IsNullOrEmpty(branch) && !string.IsNullOrEmpty(branchPassword)) branchArg += $" -betapassword {branchPassword}";

        string Args(string loginArg) => $"+force_install_dir \"{installPath}\" {loginArg} +app_update {appId}{branchArg} +quit";

        if (login == null || password == null)
        {
            await RunAsync(serverId, Args("+login anonymous"), throwOnSteamError: true);
            return;
        }

        // Giving SteamCMD the password makes it start a brand-new sign-in every time, and with Steam Guard that means an
        // approval request in the Steam app on every install/update. After one successful sign-in SteamCMD keeps a login
        // token, and "+login <name>" alone reuses it silently. Try that first; if the token is missing/expired, fall back
        // to the password (one approval, which renews the token).
        if (HasCachedLogin(login))
        {
            _sawLoginFailure = false;
            try
            {
                await RunAsync(serverId, Args($"+login {login}"), throwOnSteamError: true);
                return;
            }
            catch (InvalidOperationException) when (_sawLoginFailure)
            {
                OutputReceived?.Invoke(serverId, "[WGS] Saved Steam login not accepted — signing in with the password (approve it in the Steam app if asked).");
            }
        }
        await RunAsync(serverId, Args($"+login {login} {password}"), throwOnSteamError: true);
    }

    private bool _sawLoginFailure;

    /// <summary>True when this SteamCMD copy has already signed in as <paramref name="login"/> and remembered it.</summary>
    private bool HasCachedLogin(string login)
    {
        try
        {
            var users = Path.Combine(_steamCmdDir, "config", "loginusers.vdf");
            var cfg   = Path.Combine(_steamCmdDir, "config", "config.vdf");
            return File.Exists(users) && File.Exists(cfg)
                && File.ReadAllText(users).Contains($"\"{login}\"", StringComparison.OrdinalIgnoreCase)
                && File.ReadAllText(cfg).Contains("ConnectCache", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public async Task RunAsync(string serverId, string arguments, bool throwOnSteamError = false)
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
            ParseAndForward(serverId, e.Data);
            if (e.Data.Contains("Logging in user", StringComparison.OrdinalIgnoreCase) && e.Data.Contains("FAILED", StringComparison.OrdinalIgnoreCase)
                || e.Data.Contains("Cached credentials not found", StringComparison.OrdinalIgnoreCase))
                _sawLoginFailure = true;

            if (throwOnSteamError && e.Data.StartsWith("ERROR!", StringComparison.OrdinalIgnoreCase))
                steamError = e.Data;
        };
        _currentProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) OutputReceived?.Invoke(serverId, "[ERR] " + e.Data); };

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

        // Heartbeat — print a dot every 15 s so the user knows SteamCMD is still alive
        using var cts = new CancellationTokenSource();
        var heartbeat = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(15_000, cts.Token);
                    OutputReceived?.Invoke(serverId, "[WGS] ... still working ...");
                }
            }
            catch (TaskCanceledException) { }
        }, cts.Token);

        await _currentProcess.WaitForExitAsync();
        cts.Cancel();
        await heartbeat.ConfigureAwait(false);

        var exitCode = _currentProcess.ExitCode;

        if (steamError != null)
            throw new InvalidOperationException(steamError);

        if (exitCode != 0 && steamError == null)
            OutputReceived?.Invoke(serverId, $"[WGS] SteamCMD exited with code {exitCode}");
    }

    private void ParseAndForward(string serverId, string line)
    {
        OutputReceived?.Invoke(serverId, line);
        if (line.Contains("Update state (0x") && line.Contains("downloading") && line.Contains("%"))
        {
            var pct = ExtractPercent(line);
            if (pct >= 0) ProgressChanged?.Invoke(serverId, pct);
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

    public async Task RunWorkshopDownloadAsync(string serverId, int serverAppId, int workshopAppId, ulong itemId,
        string installPath, IProgress<(int pct, string msg)>? progress = null)
    {
        if (!IsInstalled) await DownloadSteamCmdAsync(serverId);

        progress?.Report((5, "Starting SteamCMD..."));

        var args = $"+login anonymous +workshop_download_item {workshopAppId} {itemId} +quit";
        int fakeProgress = 10;
        using var progressTimer = new System.Timers.Timer(3000);
        progressTimer.Elapsed += (_, _) =>
        {
            if (fakeProgress < 88) { fakeProgress += 8; progress?.Report((fakeProgress, "Downloading...")); }
        };
        progressTimer.Start();
        try   { await RunAsync(serverId, args); }
        finally { progressTimer.Stop(); }
        progress?.Report((100, "Done."));
    }

    public void Cancel() => _currentProcess?.Kill(entireProcessTree: true);
}
