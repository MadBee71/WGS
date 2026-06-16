using System.Collections.Concurrent;
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
    // Lock protecting Log for concurrent read (GetLog HTTP handler) vs write (process output threads)
    public readonly object LogLock = new();
    public int RestartCount { get; set; }
    public CancellationTokenSource DailyRestartCts { get; } = new();
    public nint JobHandle { get; set; } = nint.Zero;

    /// <summary>Times of recent crashes (last 10 minutes). Used for crash-loop detection.</summary>
    public List<DateTime> CrashTimes { get; } = [];

    public ServerInstance(GameServer server) => Server = server;

    public TimeSpan Uptime => StartTime.HasValue ? DateTime.Now - StartTime.Value : TimeSpan.Zero;

    private const int MaxLogLines = 500;
    public void AddToLog(ConsoleMessage msg)
    {
        lock (LogLock)
        {
            Log.Add(msg);
            while (Log.Count > MaxLogLines) Log.RemoveAt(0);
        }
    }
    public List<ConsoleMessage> GetLogSnapshot() { lock (LogLock) return Log.ToList(); }
}

public class ServerManagerService
{
    private readonly NetworkMonitorService _network;
    private readonly ConcurrentDictionary<string, ServerInstance> _running = new();

    public event Action<string, ConsoleMessage>? LogReceived;
    public event Action<string, ServerStatus>?  StatusChanged;
    /// <summary>Fired when a server has crashed too many times and auto-restart gives up.</summary>
    public event Action<string>? CrashLimitReached;
    /// <summary>Fired when a server's ports were automatically reassigned because they were in use.</summary>
    public event Action<GameServer>? PortsReassigned;

    public ServerManagerService(ConfigService config, NetworkMonitorService network)
    {
        _network = network;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAll();
    }

    public ServerInstance? GetInstance(string serverId)
        => _running.TryGetValue(serverId, out var i) ? i : null;

    /// <summary>
    /// Called once at WGS startup for every saved server. If the server has a persisted
    /// RunningPid and a matching process is still alive (WGS was closed while the game
    /// server kept running), reattach to it so Status/Stop/Kill work again. Otherwise
    /// clears the stale PID.
    /// </summary>
    public bool TryReattach(GameServer server)
    {
        if (server.RunningPid <= 0) return false;
        try
        {
            var p = Process.GetProcessById(server.RunningPid);
            if (p.HasExited)
            {
                server.RunningPid = 0;
                return false;
            }

            var plugin = GameRegistry.Get(server.GameId);
            var exeName = plugin != null ? Path.GetFileNameWithoutExtension(plugin.Executable) : null;
            if (!string.IsNullOrEmpty(exeName) && !string.Equals(p.ProcessName, exeName, StringComparison.OrdinalIgnoreCase))
            {
                // PID was recycled by an unrelated process â€” not actually our server.
                server.RunningPid = 0;
                return false;
            }

            var inst = new ServerInstance(server) { Process = p, StartTime = SafeStartTime(p) };
            try { p.EnableRaisingEvents = true; } catch { }
            p.Exited += (_, _) =>
            {
                server.RunningPid = 0;
                _running.TryRemove(server.Id, out _);
                SetStatus(server, ServerStatus.Stopped);
            };
            _running[server.Id] = inst;
            _network.RegisterServer(server.Id, p.Id);
            SetStatus(server, ServerStatus.Running);
            var msg = new ConsoleMessage { Text = $"[WGS] ðŸ”Œ Reattached to running process (PID {p.Id}) after WGS restart.", Type = ConsoleMessageType.Info };
            inst.AddToLog(msg);
            LogReceived?.Invoke(server.Id, msg);
            return true;
        }
        catch
        {
            server.RunningPid = 0;
            return false;
        }
    }

    private static DateTime? SafeStartTime(Process p)
    {
        try { return p.StartTime; } catch { return null; }
    }

    public async Task StartAsync(GameServer server)
    {
        var plugin = GameRegistry.Get(server.GameId);
        if (plugin == null) throw new InvalidOperationException("Unknown game: " + server.GameId);

        SetStatus(server, ServerStatus.Starting);

        try { Directory.CreateDirectory(server.InstallPath); } catch { }
        await plugin.PreStartAsync(server);

        // Pre-flight: kill any zombie instance of the same executable
        var inst0 = new ServerInstance(server);
        _running[server.Id] = inst0;
        var exeName = Path.GetFileNameWithoutExtension(plugin.Executable);
        if (!string.IsNullOrEmpty(exeName))
        {
            var zombies = Process.GetProcessesByName(exeName)
                                 .Where(p => { try { return !p.HasExited; } catch { return false; } })
                                 .ToList();
            foreach (var z in zombies)
            {
                try
                {
                    z.Kill(entireProcessTree: true);
                    z.WaitForExit(3000);
                    var msg = new ConsoleMessage { Text = $"[PRE-FLIGHT] ðŸ—‘ Killed leftover process {exeName} (PID {z.Id})", Type = ConsoleMessageType.Warning };
                    inst0.AddToLog(msg);
                    LogReceived?.Invoke(server.Id, msg);
                }
                catch { /* process already gone */ }
            }
            if (zombies.Count > 0)
                await Task.Delay(1000); // brief pause so OS releases ports
        }

        // Pre-flight: auto-reassign ports if any are in use
        var conflictingPorts = PortCheckerService.CheckServerPorts(server).Where(r => !r.IsAvailable).ToList();
        if (conflictingPorts.Any())
        {
            int oldGame  = server.ServerPort;
            int oldQuery = server.QueryPort;
            int oldRcon  = server.RconPort;

            // Find a free offset (up to 1000) where all ports are available
            int offset = 1;
            while (offset < 1000)
            {
                bool ok = true;
                if (!PortCheckerService.CheckPort(oldGame + offset, "UDP").IsAvailable)  { ok = false; }
                if (ok && oldQuery > 0 && !PortCheckerService.CheckPort(oldQuery + offset, "UDP").IsAvailable) { ok = false; }
                if (ok && oldRcon  > 0 && !PortCheckerService.CheckPort(oldRcon  + offset, "TCP").IsAvailable) { ok = false; }
                if (ok) break;
                offset++;
            }

            if (offset < 1000)
            {
                server.ServerPort = oldGame  + offset;
                if (oldQuery > 0) server.QueryPort = oldQuery + offset;
                if (oldRcon  > 0) server.RconPort  = oldRcon  + offset;

                var msg = new ConsoleMessage
                {
                    Text = $"[WGS] Ports in use â€” automatically reassigned: game {oldGame}â†’{server.ServerPort}" +
                           (oldQuery > 0 ? $", query {oldQuery}â†’{server.QueryPort}" : "") +
                           (oldRcon  > 0 ? $", rcon {oldRcon}â†’{server.RconPort}"   : ""),
                    Type = ConsoleMessageType.Warning
                };
                inst0.AddToLog(msg);
                LogReceived?.Invoke(server.Id, msg);
                PortsReassigned?.Invoke(server);
            }
            else
            {
                // Could not find free ports â€” warn and proceed anyway
                foreach (var r in conflictingPorts)
                {
                    var w = new ConsoleMessage { Text = $"[PRE-FLIGHT] âš  {r.Message}", Type = ConsoleMessageType.Warning };
                    inst0.AddToLog(w);
                    LogReceived?.Invoke(server.Id, w);
                }
            }
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

        // Wrap .bat/.cmd files with cmd.exe so stdout/stderr can be captured
        var ext = Path.GetExtension(exe);
        if (ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            args = $"/c \"{exe}\" {args}";
            exe  = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
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
            CreateNoWindow         = !native,
            // WindowStyle.Hidden sets STARTF_USESHOWWINDOW|SW_HIDE in STARTUPINFO.
            // This propagates to AllocConsole() AND to Windows Terminal so the tab
            // is created hidden rather than stealing focus.
            WindowStyle            = native ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
            StandardOutputEncoding = native ? null : System.Text.Encoding.UTF8,
            StandardErrorEncoding  = native ? null : System.Text.Encoding.UTF8,
        };

        // Steam environment variables â€” skip if plugin explicitly set SteamClientAppId = 0
        if (steamFileId > 0)
        {
            psi.EnvironmentVariables["SteamAppId"]          = steamEnvId.ToString();
            psi.EnvironmentVariables["SteamOverlayGameId"]  = steamEnvId.ToString();
            psi.EnvironmentVariables["SteamGameId"]         = steamEnvId.ToString();
        }

        var inst = inst0;

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // Some engines (Unity/Rust) write the same line to both stdout and stderr.
        // Suppress duplicates seen within a 200 ms window across both streams.
        var _recentLines = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();

        void AddLog(string text, ConsoleMessageType type)
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var threshold = System.Diagnostics.Stopwatch.Frequency / 5; // 200 ms
            if (_recentLines.TryGetValue(text, out var seen) && (now - seen) < threshold) return;
            _recentLines[text] = now;
            var msg = new ConsoleMessage { Text = text, Type = type };
            inst.AddToLog(msg);
            LogReceived?.Invoke(server.Id, msg);
        }

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            if (plugin.IsNoiseLine(e.Data)) return;
            AddLog(e.Data, DetectType(e.Data));
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            if (plugin.IsNoiseLine(e.Data)) return;
            AddLog(e.Data, ConsoleMessageType.Error);
        };

        proc.Exited += async (_, _) =>
        {
            try
            {
                SetStatus(server, ServerStatus.Stopped);
                server.RunningPid = 0;

                if (!server.AutoRestart || !_running.ContainsKey(server.Id))
                {
                    _running.TryRemove(server.Id, out _);
                    return;
                }

                // â”€â”€ Crash-loop detection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                var now = DateTime.Now;
                inst.CrashTimes.Add(now);
                // Forget crashes older than 10 minutes
                inst.CrashTimes.RemoveAll(t => (now - t).TotalMinutes > 10);

                int maxRetries = server.AutoRestartMaxRetries > 0 ? server.AutoRestartMaxRetries : 5;

                if (inst.CrashTimes.Count > maxRetries)
                {
                    var giveUp = new ConsoleMessage
                    {
                        Text = $"[WGS] â›” Server crashed {inst.CrashTimes.Count}Ã— in 10 min (limit {maxRetries}). Auto-restart disabled.",
                        Type = ConsoleMessageType.Error
                    };
                    inst.AddToLog(giveUp);
                    LogReceived?.Invoke(server.Id, giveUp);
                    server.AutoRestart = false;
                    SetStatus(server, ServerStatus.Error);
                    _running.TryRemove(server.Id, out _);
                    CrashLimitReached?.Invoke(server.Id);
                    return;
                }

                int delaySec = server.AutoRestartDelaySec > 0 ? server.AutoRestartDelaySec : 10;
                inst.RestartCount++;
                var delayMsg = new ConsoleMessage
                {
                    Text = $"[WGS] âš  Server stopped unexpectedly (crash #{inst.CrashTimes.Count}/{maxRetries}). Restarting in {delaySec}s...",
                    Type = ConsoleMessageType.Warning
                };
                inst.AddToLog(delayMsg);
                LogReceived?.Invoke(server.Id, delayMsg);

                await Task.Delay(delaySec * 1000);

                // Re-check: user might have disabled auto-restart during the delay
                if (!server.AutoRestart || !_running.ContainsKey(server.Id))
                {
                    _running.TryRemove(server.Id, out _);
                    return;
                }

                await StartAsync(server);
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
            _running.TryRemove(server.Id, out _);
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

            // Output is captured in WGS â€” hide any window the process creates.
            // CreateNoWindow only suppresses the console host; some servers (e.g. Rust)
            // still create a Win32 window via AllocConsole/CreateWindow internally.
            // Poll until the window appears, then hide it completely.
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 30 && !proc.HasExited; i++) // up to 15 seconds
                {
                    await Task.Delay(500);
                    try
                    {
                        proc.Refresh();
                        var hwnd = proc.MainWindowHandle;
                        if (hwnd != IntPtr.Zero)
                        {
                            ShowWindow(hwnd, SW_HIDE);
                            break;
                        }
                    }
                    catch { break; }
                }
            });
        }
        else
        {
            // Native-console server: has its own useful console window.
            // Minimize to taskbar without stealing focus â€” user can open it with Console button.
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 20 && !proc.HasExited; i++) // up to 10 seconds
                {
                    await Task.Delay(500);
                    try
                    {
                        proc.Refresh();
                        var hwnd = proc.MainWindowHandle;
                        if (hwnd != IntPtr.Zero)
                        {
                            ShowWindow(hwnd, SW_SHOWMINNOACTIVE);
                            break;
                        }
                    }
                    catch { break; }
                }
            });
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
        server.RunningPid  = proc.Id;

        // Apply RAM limit via Windows Job Object
        if (server.MaxRamMb > 0)
            inst.JobHandle = JobObjectService.ApplyRamLimit(proc, server.MaxRamMb);

        // Schedule daily restart if enabled
        if (server.DailyRestartEnabled)
            _ = RunDailyRestartAsync(server, inst);

        // RekisterÃ¶i kaistaseurantaan
        _network.RegisterServer(server.Id, proc.Id);

        // Add firewall rules
        if (server.FirewallAutoManage)
            FirewallService.AddRules(server);

        SetStatus(server, ServerStatus.Running);
    }

    public async Task StopAsync(GameServer server)
    {
        if (!_running.TryGetValue(server.Id, out var inst))
        {
            // WGS may have been restarted while this server kept running â€” fall back
            // to killing the orphaned PID we persisted to disk.
            await KillOrphanedPidAsync(server);
            return;
        }
        SetStatus(server, ServerStatus.Stopping);

        var plugin = GameRegistry.Get(server.GameId);
        var stopCmd = plugin?.GetStopCommand(server);

        if (stopCmd != null && inst.Process?.HasExited == false)
        {
            try { await inst.Process.StandardInput.WriteLineAsync(stopCmd); }
            catch { }
            await Task.Delay(5000);
        }

        inst.DailyRestartCts.Cancel();
        _running.TryRemove(server.Id, out _);
        _network.UnregisterServer(server.Id);
        if (server.FirewallAutoManage) FirewallService.RemoveRules(server);

        if (inst.Process?.HasExited == false)
            inst.Process.Kill(entireProcessTree: true);

        server.RunningPid = 0;
        SetStatus(server, ServerStatus.Stopped);
    }

    /// <summary>
    /// Kills a process by the PID persisted on the server model, used when WGS lost its
    /// in-memory ServerInstance (e.g. after WGS itself was restarted) but the game server
    /// process is still alive in the background.
    /// </summary>
    private Task KillOrphanedPidAsync(GameServer server)
    {
        SetStatus(server, ServerStatus.Stopping);
        if (server.RunningPid > 0)
        {
            try
            {
                var p = Process.GetProcessById(server.RunningPid);
                if (!p.HasExited) p.Kill(entireProcessTree: true);
            }
            catch { /* already gone */ }
        }
        server.RunningPid = 0;
        if (server.FirewallAutoManage) FirewallService.RemoveRules(server);
        SetStatus(server, ServerStatus.Stopped);
        return Task.CompletedTask;
    }

    public async Task SendCommandAsync(string serverId, string command)
    {
        if (!_running.TryGetValue(serverId, out var inst)) return;
        var plugin = GameRegistry.Get(inst.Server.GameId);

        // Plugins whose process has no redirected stdin (native console) must route
        // commands through their own REST/RCON mechanism instead.
        if (plugin is IRestCommandPlugin restCmd)
        {
            var (handled, response) = await restCmd.TrySendRestCommandAsync(inst.Server, command);
            if (handled)
            {
                if (!string.IsNullOrEmpty(response))
                {
                    var msg = new ConsoleMessage { Text = response, Type = ConsoleMessageType.Info };
                    inst.AddToLog(msg);
                    LogReceived?.Invoke(serverId, msg);
                }
                return;
            }
        }

        if (plugin?.UseNativeConsole != true && inst.Process != null)
            await inst.Process.StandardInput.WriteLineAsync(command);
    }

    public void SendCommand(string serverId, string command)
        => _ = SendCommandAsync(serverId, command);

    public bool IsRunning(string serverId)
        => _running.TryGetValue(serverId, out var inst) && inst.Process?.HasExited == false;

    public Task KillAsync(GameServer server)
    {
        if (!_running.TryGetValue(server.Id, out var inst)) return KillOrphanedPidAsync(server);
        SetStatus(server, ServerStatus.Stopping);
        inst.DailyRestartCts.Cancel();
        JobObjectService.ReleaseJob(inst.JobHandle);
        _running.TryRemove(server.Id, out _);
        _network.UnregisterServer(server.Id);
        if (server.FirewallAutoManage) FirewallService.RemoveRules(server);
        try { inst.Process?.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* process already dead â€” swallow */ }
        server.RunningPid = 0;
        SetStatus(server, ServerStatus.Stopped);
        return Task.CompletedTask;
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
    private const int SW_HIDE            = 0; // hide window completely
    private const int SW_SHOWMINNOACTIVE = 7; // minimize without stealing focus
    private const int SW_RESTORE         = 9;

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

    private async Task RunDailyRestartAsync(GameServer server, ServerInstance inst)
    {
        while (_running.TryGetValue(server.Id, out var current) && current == inst && server.DailyRestartEnabled)
        {
            var now = DateTime.Now;
            var target = now.Date + server.DailyRestartTime;
            if (target <= now)
                target = target.AddDays(1);

            var delay = target - now;
            try { await Task.Delay(delay, inst.DailyRestartCts.Token); }
            catch (TaskCanceledException) { return; }

            if (!_running.TryGetValue(server.Id, out var c) || c != inst || !server.DailyRestartEnabled)
                return;

            var msg = new ConsoleMessage
            {
                Text = $"[WGS] Daily restart triggered at {server.DailyRestartTime:hh\\:mm}",
                Type = ConsoleMessageType.Warning
            };
            inst.AddToLog(msg);
            LogReceived?.Invoke(server.Id, msg);

            await StopAsync(server);
            await Task.Delay(3000);
            await StartAsync(server);
            return; // new instance will spawn its own RunDailyRestartAsync
        }
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

