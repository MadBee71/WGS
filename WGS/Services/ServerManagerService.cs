using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

public class ServerInstance
{
    public GameServer Server { get; }
    public Process? Process { get; set; }
    public DateTime? StartTime { get; set; }
    public ObservableCollection<ConsoleMessage> Log { get; } = [];
    public int RestartCount { get; set; }

    public ServerInstance(GameServer server) => Server = server;

    public TimeSpan Uptime => StartTime.HasValue ? DateTime.Now - StartTime.Value : TimeSpan.Zero;
}

public class ServerManagerService
{
    private readonly ConfigService _config;
    private readonly Dictionary<string, ServerInstance> _running = new();

    public event Action<string, ConsoleMessage>? LogReceived;
    public event Action<string, ServerStatus>? StatusChanged;

    public ServerManagerService(ConfigService config)
    {
        _config = config;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAll();
    }

    public ServerInstance? GetInstance(string serverId)
        => _running.TryGetValue(serverId, out var i) ? i : null;

    public async Task StartAsync(GameServer server)
    {
        var plugin = GameRegistry.Get(server.GameId);
        if (plugin == null) throw new InvalidOperationException("Unknown game: " + server.GameId);

        SetStatus(server, ServerStatus.Starting);

        try { Directory.CreateDirectory(server.InstallPath); } catch { }
        await plugin.PreStartAsync(server);

        // Pre-flight: warn if ports are already in use
        var inst0 = new ServerInstance(server);
        _running[server.Id] = inst0;
        foreach (var r in PortCheckerService.CheckServerPorts(server).Where(r => !r.IsAvailable))
        {
            var w = new ConsoleMessage { Text = $"[PRE-FLIGHT] ⚠ {r.Message}", Type = ConsoleMessageType.Warning };
            inst0.Log.Add(w);
            LogReceived?.Invoke(server.Id, w);
        }

        var args = plugin.BuildStartArguments(server);
        if (!string.IsNullOrWhiteSpace(server.Gslt))       args += $" +sv_setsteamaccount {server.Gslt}";
        if (!string.IsNullOrWhiteSpace(server.CustomArgs)) args += $" {server.CustomArgs.Replace(Environment.NewLine, " ")}";
        var exe  = Path.Combine(server.InstallPath, plugin.Executable);

        // If primary exe missing, try auto-detect from install folder
        if (!File.Exists(exe))
        {
            var found = TryFindExecutable(server.InstallPath, plugin.Executable);
            if (found != null)
                exe = found;
            else
                throw new FileNotFoundException("Server executable not found in: " + server.InstallPath);
        }

        // WorkingDirectory must be the exe's own folder (not just InstallPath)
        var exeDir = Path.GetDirectoryName(exe) ?? server.InstallPath;

        // SteamClientAppId == 0 means "don't write steam_appid.txt or set Steam env vars"
        // SteamClientAppId > 0 means explicit override; otherwise fall back to SteamAppId
        var steamFileId = plugin.SteamClientAppId;
        var steamEnvId  = plugin.SteamClientAppId > 0 ? plugin.SteamClientAppId : plugin.SteamAppId;

        if (steamFileId > 0)
        {
            try { File.WriteAllText(Path.Combine(exeDir, "steam_appid.txt"), steamFileId.ToString()); }
            catch { }
        }

        var native = plugin.UseNativeConsole;
        var psi = new ProcessStartInfo
        {
            FileName               = exe,
            Arguments              = args,
            WorkingDirectory       = exeDir,
            UseShellExecute        = false,
            RedirectStandardOutput = !native,
            RedirectStandardError  = !native,
            RedirectStandardInput  = !native,
            CreateNoWindow         = false,
            StandardOutputEncoding = native ? null : System.Text.Encoding.UTF8,
            StandardErrorEncoding  = native ? null : System.Text.Encoding.UTF8,
        };

        // Steam environment variables — skip if plugin explicitly set SteamClientAppId = 0
        if (steamFileId > 0)
        {
            psi.EnvironmentVariables["SteamAppId"]          = steamEnvId.ToString();
            psi.EnvironmentVariables["SteamOverlayGameId"]  = steamEnvId.ToString();
            psi.EnvironmentVariables["SteamGameId"]         = steamEnvId.ToString();
        }

        var inst = inst0;

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            var msg = new ConsoleMessage { Text = e.Data, Type = DetectType(e.Data) };
            inst.Log.Add(msg);
            LogReceived?.Invoke(server.Id, msg);
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            var msg = new ConsoleMessage { Text = e.Data, Type = ConsoleMessageType.Error };
            inst.Log.Add(msg);
            LogReceived?.Invoke(server.Id, msg);
        };

        proc.Exited += async (_, _) =>
        {
            try
            {
                SetStatus(server, ServerStatus.Stopped);
                if (server.AutoRestart && _running.ContainsKey(server.Id))
                {
                    await Task.Delay(5000);
                    if (server.AutoRestart)
                    {
                        inst.RestartCount++;
                        await StartAsync(server);
                    }
                }
                else
                {
                    _running.Remove(server.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WGS] Exited handler error for {server.Id}: {ex.Message}");
            }
        };

        try
        {
            proc.Start();
        }
        catch (Win32Exception ex)
        {
            _running.Remove(server.Id);
            SetStatus(server, ServerStatus.Error);
            var errMsg = new ConsoleMessage
            {
                Text = $"[ERR] Failed to start server process: {ex.Message}",
                Type = ConsoleMessageType.Error
            };
            LogReceived?.Invoke(server.Id, errMsg);
            throw;
        }

        if (!native)
        {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        // Apply CPU affinity
        if (server.CpuAffinityMask != 0)
        {
            try { proc.ProcessorAffinity = (IntPtr)server.CpuAffinityMask; } catch { }
        }

        // Apply process priority
        try
        {
            proc.PriorityClass = server.ProcessPriority switch
            {
                "AboveNormal" => ProcessPriorityClass.AboveNormal,
                "High"        => ProcessPriorityClass.High,
                "BelowNormal" => ProcessPriorityClass.BelowNormal,
                "RealTime"    => ProcessPriorityClass.RealTime,
                _             => ProcessPriorityClass.Normal,
            };
        }
        catch { }

        inst.Process   = proc;
        inst.StartTime = DateTime.Now;
        server.LastStarted = DateTime.Now;

        // Add firewall rules
        if (server.FirewallAutoManage)
            FirewallService.AddRules(server);

        SetStatus(server, ServerStatus.Running);
        await Task.CompletedTask;
    }

    public async Task StopAsync(GameServer server)
    {
        if (!_running.TryGetValue(server.Id, out var inst)) return;
        SetStatus(server, ServerStatus.Stopping);

        var plugin = GameRegistry.Get(server.GameId);
        var stopCmd = plugin?.GetStopCommand(server);

        if (stopCmd != null && inst.Process?.HasExited == false)
        {
            try { await inst.Process.StandardInput.WriteLineAsync(stopCmd); }
            catch { }
            await Task.Delay(5000);
        }

        if (inst.Process?.HasExited == false)
            inst.Process.Kill(entireProcessTree: true);

        server.AutoRestart = false;
        _running.Remove(server.Id);
        if (server.FirewallAutoManage) FirewallService.RemoveRules(server);
        SetStatus(server, ServerStatus.Stopped);
    }

    public async Task SendCommandAsync(string serverId, string command)
    {
        if (_running.TryGetValue(serverId, out var inst) && inst.Process?.StandardInput != null)
            await inst.Process.StandardInput.WriteLineAsync(command);
    }

    public void SendCommand(string serverId, string command)
    {
        if (_running.TryGetValue(serverId, out var inst) && inst.Process?.StandardInput != null)
            _ = inst.Process.StandardInput.WriteLineAsync(command)
                    .ContinueWith(t => { /* swallow */ }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public bool IsRunning(string serverId)
        => _running.TryGetValue(serverId, out var inst) && inst.Process?.HasExited == false;

    public async Task KillAsync(GameServer server)
    {
        if (!_running.TryGetValue(server.Id, out var inst)) return;
        SetStatus(server, ServerStatus.Stopping);
        server.AutoRestart = false;
        try { inst.Process?.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* process already dead — swallow */ }
        _running.Remove(server.Id);
        if (server.FirewallAutoManage) FirewallService.RemoveRules(server);
        SetStatus(server, ServerStatus.Stopped);
        await Task.CompletedTask;
    }

    public void KillAll()
    {
        foreach (var id in _running.Keys.ToList())
        {
            try { _running[id].Process?.Kill(entireProcessTree: true); } catch { }
        }
        _running.Clear();
    }

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    private const int SW_RESTORE = 9;

    public void ShowWindow(GameServer server)
    {
        if (!_running.TryGetValue(server.Id, out var inst)) return;
        var proc = inst.Process;
        if (proc == null || proc.HasExited) return;
        try
        {
            var hwnd = proc.MainWindowHandle;
            if (hwnd == IntPtr.Zero) return;
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
        catch { }
    }

    private void SetStatus(GameServer server, ServerStatus status)
    {
        server.Status = status;
        StatusChanged?.Invoke(server.Id, status);
    }

    private static string? TryFindExecutable(string installPath, string hintExe)
    {
        if (!Directory.Exists(installPath)) return null;

        // 1. exact in root
        var root = Path.Combine(installPath, hintExe);
        if (File.Exists(root)) return root;

        // 2. any *server*.exe or *dedicated*.exe in root
        foreach (var f in Directory.GetFiles(installPath, "*.exe"))
        {
            var n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
            if (n.Contains("server") || n.Contains("dedicated")) return f;
        }

        // 3. recurse one level
        foreach (var dir in Directory.GetDirectories(installPath))
        {
            var sub = Path.Combine(dir, hintExe);
            if (File.Exists(sub)) return sub;
            foreach (var f in Directory.GetFiles(dir, "*.exe"))
            {
                var n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                if (n.Contains("server") || n.Contains("dedicated")) return f;
            }
        }

        // 4. any .exe at all
        var any = Directory.GetFiles(installPath, "*.exe").FirstOrDefault();
        return any;
    }

    private static ConsoleMessageType DetectType(string line)
    {
        var l = line.ToLowerInvariant();
        if (l.Contains("error") || l.Contains("exception") || l.Contains("fatal")) return ConsoleMessageType.Error;
        if (l.Contains("warn")) return ConsoleMessageType.Warning;
        return ConsoleMessageType.Info;
    }
}
