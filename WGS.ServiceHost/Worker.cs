using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WGS.Services;
using WGS.Models;

namespace WGS.ServiceHost;

/// <summary>
/// The actual background service: composes the same Services/* classes the WPF app uses,
/// wires them into a LocalServerBackend, and wires WebApiService's full delegate surface
/// (existing + the ones added for IServerBackend parity) to it. A WPF client in "service
/// mode" (Settings → Service mode) talks to this over HTTP via RemoteServerBackend, at
/// http://localhost:{Port} with the token logged/persisted below.
/// </summary>
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    public const int Port = 8766;

    private WebApiService? _webApi;
    private ScheduledTaskService? _scheduler;
    private NetworkMonitorService? _network;
    private WakeOnDemandService? _wake;
    private ServerHealthService? _health;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WGS.ServiceHost starting");

        var config       = new ConfigService();
        var network      = new NetworkMonitorService();
        var manager      = new ServerManagerService(config, network);
        var steamCmd     = new SteamCmdService(config);
        var backup       = new BackupService(config);
        var notify       = new NotificationService(config);
        var mods         = new ModManagerService();
        var sourceMod    = new SourceModService();
        var configEditor = new ConfigEditorService(config);
        var workshopDb   = new WorkshopDbService(config);
        var workshop     = new SteamWorkshopService(steamCmd, workshopDb);
        var templates    = new TemplateService(config);
        var scheduler    = new ScheduledTaskService(config, manager, backup, notify);
        var hygiene      = new ServerHygieneService();
        var presets      = new ConfigPresetService();
        var users        = new UserService(config);
        var metrics      = new SystemMetricsService();
        _scheduler = scheduler;
        _network   = network;

        var backend = new LocalServerBackend(manager, steamCmd, backup, notify, config, mods, sourceMod,
            configEditor, workshop, workshopDb, templates, scheduler, hygiene, presets);

        // servers.json only holds settings; the live status belongs to the manager. Without it Install/Stop/Kill saw a stale
        // saved status (an Update of a running server did not stop it first).
        GameServer? FindServer(string id)
        {
            var found = config.LoadServers().FirstOrDefault(s => s.Id == id);
            if (found != null) found.Status = manager.GetServer(id)?.Status ?? ServerStatus.Stopped;
            return found;
        }

        // Route scheduler/daily-restart Start/Stop/Restart through the backend (same as the WPF
        // client's MainViewModel wiring) so BackupOnShutdown/BackupOnStart/UpdateOnStart are honored
        // here too instead of falling through to ServerManagerService's bare stop/start. No WPF
        // Dispatcher to worry about in this headless host — direct calls are fine.
        scheduler.StartServer   = id => backend.StartAsync(FindServer(id)!);
        scheduler.StopServer    = id => backend.StopAsync(FindServer(id)!);
        scheduler.RestartServer = async id =>
        {
            var s = FindServer(id)!;
            await backend.StopAsync(s);
            await manager.WaitForPortsFreeAsync(s);
            await backend.StartAsync(s);
        };
        manager.FullRestartHook = async s =>
        {
            await backend.StopAsync(s);
            await manager.WaitForPortsFreeAsync(s);
            await backend.StartAsync(s);
        };
        // Crash-loop auto-restart only needs the "start" half — see StartOnlyHook's doc comment.
        manager.StartOnlyHook = s => backend.StartAsync(s);

        // Remember each running server's PID in servers.json (the WPF client does this through its view models), so a
        // restarted service can reattach to servers that kept running — see the TryReattach loop below.
        var pidLock = new object();
        manager.StatusChanged += (id, status) =>
        {
            if (status is not (ServerStatus.Running or ServerStatus.Stopped or ServerStatus.Error)) return;
            try
            {
                lock (pidLock)
                {
                    var all   = config.LoadServers();
                    var entry = all.FirstOrDefault(x => x.Id == id);
                    if (entry == null) return;
                    var pid = status == ServerStatus.Running ? manager.GetServer(id)?.RunningPid ?? 0 : 0;
                    if (entry.RunningPid == pid) return;
                    entry.RunningPid = pid;
                    config.SaveServers(all);
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Could not save PID for {Id}", id); }
        };

        // What the WPF window does when a server changes state (Discord start/stop/crash messages, UPnP ports, Wake on
        // Demand / idle shutdown) now runs here too, so servers handled by the service behave like in the normal window.
        var upnp         = new UPnPService();
        var wake         = new WakeOnDemandService(manager, backend, backup, notify);
        var healthCheck  = new ServerHealthService(manager, backend, notify, config);
        var logWatcher   = new LogWatcherService(manager, backend, notify);
        _wake = wake; _health = healthCheck;
        healthCheck.StartMonitoring();

        // Rules and settings can change in the client while a server runs, so the service reads the server's saved
        // settings again at most every 30 seconds for the per-line log watcher.
        var watchCache = new System.Collections.Concurrent.ConcurrentDictionary<string, (GameServer Server, DateTime At)>();
        manager.LogReceived += (id, msg) =>
        {
            try
            {
                var cached = watchCache.TryGetValue(id, out var c) && DateTime.UtcNow - c.At < TimeSpan.FromSeconds(30)
                    ? c.Server : null;
                if (cached == null)
                {
                    cached = FindServer(id);
                    if (cached == null) return;
                    watchCache[id] = (cached, DateTime.UtcNow);
                }
                logWatcher.CheckLine(cached, msg.Text);
            }
            catch (Exception ex) { logger.LogDebug(ex, "Log watcher check failed"); }
        };

        // Reattaching to servers that kept running fires "Running" events too — no "started" message for those.
        var startupDone = false;
        manager.StatusChanged += (id, status) =>
        {
            try
            {
                var server = FindServer(id) ?? manager.GetServer(id);
                if (server == null) return;
                if (startupDone) _ = notify.NotifyServerStatusAsync(manager.GetServer(id) ?? server, status);
                if (status == ServerStatus.Running)
                {
                    if (config.EnableUPnP) _ = upnp.AddPortsForServerAsync(server);
                    wake.Disarm(id);
                    wake.ArmIdleShutdown(server);
                }
                else if (status is ServerStatus.Stopped or ServerStatus.Error)
                {
                    if (config.EnableUPnP) _ = upnp.RemovePortsForServerAsync(server);
                    wake.DisarmIdleShutdown(id);
                    wake.Arm(server);
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Status change handling failed for {Id}", id); }
        };

        async Task<string?> Try(Func<Task> op)
        {
            try { await op(); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        async Task<(T? value, string? error)> TryValue<T>(Func<Task<T>> op)
        {
            try { return (await op(), null); }
            catch (Exception ex) { return (default, ex.Message); }
        }

        // Servers that kept running while the service was stopped/restarted: pick them up again (as the WPF client does at
        // startup), otherwise they would show as stopped and could no longer be stopped from here.
        // Servers marked "Auto-start" are started here when they were not found still running — after a reboot nothing
        // else would start them (the WPF client no longer does in service mode).
        var autoStartServers = new List<GameServer>();
        logger.LogInformation("Service starting as {User}, version {Version}, data folder {Dir}",
            Environment.UserName, typeof(Worker).Assembly.GetName().Version, config.AppDataPath);
        foreach (var s in config.LoadServers())
        {
            s.Status = ServerStatus.Stopped;
            var reattached = false;
            try { reattached = manager.TryReattach(s); } catch (Exception ex) { logger.LogWarning(ex, "Could not reattach {Server}", s.DisplayName); }
            logger.LogInformation("Server {Server}: AutoStart={Auto}, reattached={Re}, install path {Path} (exists: {Exists})",
                s.DisplayName, s.AutoStart, reattached, s.InstallPath, Directory.Exists(s.InstallPath));
            if (s.AutoStart && !reattached) autoStartServers.Add(s);
            else if (!reattached && s.WakeOnDemand) wake.Arm(s);
        }
        startupDone = true;

        var webApi = new WebApiService { Users = users, DashboardEnabled = true };
        _webApi = webApi;

        // FiveM/RedM install can't show Views.BuildChannelDialog here (no WPF) — pause via the
        // web dashboard/API instead. See LocalServerBackend.InstallFromManualDownloadAsync.
        backend.RegisterBuildChannelChoice = webApi.RegisterPendingBuildChannelChoice;

        // ── Pre-existing delegate surface ───────────────────────────────────────────────────
        // servers.json only holds settings: Status/CurrentPlayers are live state on the manager's instances. Listing the freshly
        // loaded objects as-is showed every server as "NotInstalled" with 0 players, running or not.
        webApi.GetServers    = () => config.LoadServers().Select(s =>
        {
            var live = manager.GetServer(s.Id);
            s.Status         = live?.Status ?? ServerStatus.Stopped;
            s.CurrentPlayers = live?.CurrentPlayers ?? 0;
            return s;
        }).ToList();
        webApi.StartServer   = id => backend.StartAsync(FindServer(id)!);
        webApi.StopServer    = id => backend.StopAsync(FindServer(id)!);
        webApi.KillServer    = id => backend.KillAsync(FindServer(id)!);
        webApi.RestartServer = async id => { var s = FindServer(id)!; await backend.StopAsync(s); await Task.Delay(3000); await backend.StartAsync(s); };
        webApi.UpdateServer  = id => backend.InstallAsync(FindServer(id)!);
        webApi.BackupServer  = async id => { await backend.CreateBackupAsync(FindServer(id)!); };
        webApi.SendCmd       = (id, cmd) => backend.SendConsoleCommandAsync(id, cmd);
        webApi.GetMetrics    = () => metrics.Current;
        webApi.GetNetwork    = () => (network.CurrentBytesInPerSec, network.CurrentBytesOutPerSec);
        webApi.GetLog        = (id, offset) => backend.GetLogAsync(id, offset).GetAwaiter().GetResult();
        webApi.GetUptime     = id => manager.GetInstance(id)?.Uptime is TimeSpan t && t > TimeSpan.Zero
            ? $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}" : null;
        webApi.GetFullLog    = id => manager.GetInstance(id)?.GetLogSnapshot().Select(m => m.Text) ?? [];
        webApi.GetBackups    = id =>
        {
            var s = FindServer(id);
            return s == null ? [] : backup.GetBackupsForServer(s).Select(b =>
                (fileName: Path.GetFileName(b.FilePath), sizeText: b.SizeText, createdAt: b.CreatedAt));
        };
        webApi.RestoreBackup = (id, fileName) => backend.RestoreBackupAsync(FindServer(id)!, fileName)
            .ContinueWith(t => t.IsFaulted ? t.Exception!.InnerException!.Message : null);
        webApi.SaveNote = async (id, notes) =>
        {
            var s = FindServer(id);
            if (s == null) return "Server not found";
            s.Notes = notes;
            config.SaveServers(config.LoadServers().Select(x => x.Id == id ? s : x));
            return null;
        };
        webApi.GetScheduledTasks = () => scheduler.Tasks;
        webApi.RunScheduledTask  = async id => { try { await scheduler.ExecuteNowAsync(id); return null; } catch (Exception ex) { return ex.Message; } };

        // ── IServerBackend-parity delegate surface ──────────────────────────────────────────
        webApi.GetConfigFiles        = id => backend.GetConfigFilesAsync(FindServer(id)!);
        webApi.ReadConfigFile        = (id, path) => backend.ReadConfigFileAsync(FindServer(id)!, path);
        webApi.SaveConfigFile        = (id, path, content) => Try(() => backend.SaveConfigFileAsync(FindServer(id)!, path, content));
        webApi.GetConfigSnapshots    = (id, path) => backend.GetConfigSnapshotsAsync(FindServer(id)!, path);
        webApi.RestoreConfigSnapshot = (id, snapshotPath) => Try(() => backend.RestoreConfigSnapshotAsync(FindServer(id)!, new ConfigSnapshot { FilePath = snapshotPath }));
        webApi.ApplyPreset = (id, name, values) => Try(async () =>
        {
            var s = FindServer(id)!;
            var preset = presets.GetPresetsForGame(s.GameId).FirstOrDefault(p => p.Name == name)
                ?? throw new InvalidOperationException("Preset not found: " + name);
            await backend.ApplyPresetAsync(s, preset);
        });
        webApi.InstallModFramework = (id, framework) => Try(() => backend.InstallModFrameworkAsync(FindServer(id)!, framework, new Progress<(int, string)>()));
        webApi.GetThunderstoreMods = (id, check) => backend.GetThunderstoreModsAsync(FindServer(id)!, check);
        webApi.ThunderstoreAction  = (id, action, value, enabled) =>
        {
            var s = FindServer(id) ?? throw new InvalidOperationException("Server not found");
            // Use the live status, not the one in servers.json, so a running server is refused.
            s.Status = manager.GetServer(id)?.Status ?? ServerStatus.Stopped;
            return action switch
            {
                "install"   => backend.InstallThunderstoreAsync(s, value),
                "uninstall" => backend.UninstallThunderstoreAsync(s, value),
                "toggle"    => backend.SetThunderstoreModEnabledAsync(s, value, enabled),
                _           => backend.UpdateThunderstoreModsAsync(s),
            };
        };
        webApi.GetSourceModPlugins = id => backend.GetSourceModPluginsAsync(FindServer(id)!);
        webApi.SetSourceModPluginEnabled = (id, fileName, enabled) => backend.SetSourceModPluginEnabledAsync(FindServer(id)!, fileName, enabled);
        webApi.GetWorkshopMods       = id => backend.GetWorkshopModsAsync(FindServer(id)!);
        webApi.SearchWorkshop        = (id, q) => backend.SearchWorkshopAsync(FindServer(id)!, q);
        webApi.InstallWorkshopItem   = (id, itemId) => Try(() => backend.InstallWorkshopItemAsync(FindServer(id)!, itemId));
        webApi.UninstallWorkshopItem = (id, modId) => Try(async () =>
        {
            var s = FindServer(id)!;
            var mod = workshopDb.GetModsForServer(s.Id).FirstOrDefault(m => m.ModId == modId)
                ?? throw new InvalidOperationException("Mod not found: " + modId);
            await backend.UninstallWorkshopItemAsync(s, mod);
        });
        webApi.SetWorkshopModEnabled = (id, modId, enabled) => Try(async () =>
        {
            var s = FindServer(id)!;
            var mod = workshopDb.GetModsForServer(s.Id).FirstOrDefault(m => m.ModId == modId)
                ?? throw new InvalidOperationException("Mod not found: " + modId);
            await backend.SetWorkshopModEnabledAsync(s, mod, enabled);
        });
        webApi.CheckOutdatedMods = id => backend.CheckOutdatedModsAsync(FindServer(id)!);
        webApi.UpdateWorkshopMods = (id, modIds) => Try(async () =>
        {
            var s = FindServer(id)!;
            IEnumerable<WorkshopMod>? only = modIds == null ? null
                : workshopDb.GetModsForServer(s.Id).Where(m => modIds.Contains(m.ModId));
            await backend.UpdateModsAsync(s, only);
        });
        webApi.CleanupJunkFiles = id => backend.CleanupJunkFilesAsync(FindServer(id)!);
        webApi.SaveAsTemplate = (id, name, description, category, tags) => backend.SaveAsTemplateAsync(FindServer(id)!, name, description, category, tags);
        webApi.ApplyTemplate = (id, templateId) => Try(async () =>
        {
            var s = FindServer(id)!;
            var t = templates.ForGame(s.GameId).FirstOrDefault(t => t.Id == templateId)
                ?? throw new InvalidOperationException("Template not found: " + templateId);
            await backend.ApplyTemplateAsync(s, t);
        });
        webApi.CloneTemplate = async templateId =>
        {
            var (t, err) = await TryValue(async () =>
            {
                var src = templates.All.FirstOrDefault(x => x.Id == templateId)
                    ?? throw new InvalidOperationException("Template not found: " + templateId);
                return await backend.CloneTemplateAsync(src);
            });
            return err != null ? throw new InvalidOperationException(err) : t;
        };
        webApi.DeleteTemplate = templateId => Try(async () =>
        {
            var t = templates.All.FirstOrDefault(x => x.Id == templateId)
                ?? throw new InvalidOperationException("Template not found: " + templateId);
            await backend.DeleteTemplateAsync(t);
        });
        webApi.AddScheduledTask    = task => backend.AddScheduledTaskAsync(task);
        webApi.RemoveScheduledTask = taskId => Try(async () =>
        {
            var t = scheduler.Tasks.FirstOrDefault(x => x.Id == taskId)
                ?? throw new InvalidOperationException("Task not found: " + taskId);
            await backend.RemoveScheduledTaskAsync(t);
        });
        webApi.AddQuickCommand    = (id, qc)   => backend.AddQuickCommandAsync(FindServer(id)!, qc);
        webApi.RemoveQuickCommand = (id, qc)   => backend.RemoveQuickCommandAsync(FindServer(id)!, qc);
        webApi.AddLogWatchRule    = (id, rule) => backend.AddLogWatchRuleAsync(FindServer(id)!, rule);
        webApi.RemoveLogWatchRule = (id, rule) => backend.RemoveLogWatchRuleAsync(FindServer(id)!, rule);

        webApi.CreateBackupDetailed = async id =>
        {
            var entry = await backend.CreateBackupAsync(FindServer(id)!);
            return new WebApiService.BackupDetail(entry.FilePath, Path.GetFileName(entry.FilePath), entry.ServerName,
                entry.CreatedAt, entry.SizeBytes, entry.IsIncremental, entry.BaseFilePath);
        };
        webApi.DeleteBackup = (id, fileName) => Try(() => backend.DeleteBackupAsync(FindServer(id)!, fileName));
        webApi.CleanupBackups = async id =>
        {
            var deleted = await backend.CleanupBackupsAsync(FindServer(id)!);
            return deleted.Select(entry => new WebApiService.BackupDetail(entry.FilePath, Path.GetFileName(entry.FilePath),
                entry.ServerName, entry.CreatedAt, entry.SizeBytes, entry.IsIncremental, entry.BaseFilePath)).ToList();
        };

        webApi.GetOnlinePlayers = id => backend.GetOnlinePlayersAsync(manager.GetServer(id) ?? FindServer(id)!).GetAwaiter().GetResult();

        // Persistent token: a real service must hand out the same token across restarts, since
        // a WPF client's "Service mode" setting (Settings → Service mode → Access token) has to
        // be typed in once and keep working — a fresh random token every start (fine for the
        // earlier interactive smoke-test build) would break that. Client and service share the
        // same WGS_Data\settings.json only when co-located in the same folder (see ConfigService's
        // "data always next to exe" rule) — that's what makes this the right value to reuse.
        if (string.IsNullOrWhiteSpace(config.WebApiToken))
        {
            config.WebApiToken = Guid.NewGuid().ToString("N");
            config.Save();
        }

        webApi.Start(Port, config.WebApiToken);

        // Everything is wired now — start the "Auto-start" servers (each in its own task, like the WPF client did).
        foreach (var server in autoStartServers)
        {
            var srv = server;
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Auto-starting {Server}", srv.DisplayName);
                    await backend.StartAsync(srv);
                    logger.LogInformation("Auto-start of {Server} finished (status {Status})", srv.DisplayName, manager.GetServer(srv.Id)?.Status);
                }
                catch (Exception ex) { logger.LogWarning(ex, "Auto-start of {Server} failed", srv.DisplayName); }
            }, stoppingToken);
        }

        var serverCount = config.LoadServers().Count;
        logger.LogInformation("servers.json: {Path} ({Count} server(s))", config.ServersFile, serverCount);
        logger.LogInformation("Listening on http://localhost:{Port}/ (all interfaces: {AllInterfaces})", Port, webApi.BoundToAllInterfaces);
        Console.WriteLine("WGS.ServiceHost running.");
        Console.WriteLine($"servers.json: {config.ServersFile}  ({serverCount} server(s))");
        Console.WriteLine($"Listening on http://localhost:{Port}/  (all interfaces: {webApi.BoundToAllInterfaces})");
        Console.WriteLine($"Token: {config.WebApiToken}");
        Console.WriteLine("(This token is saved in settings.json — point a WPF client's Settings -> Service mode at this URL and token.)");

        // The WPF client polls players per open server (ServerViewModel.FetchOnlinePlayersAsync); a headless
        // service has no view models, so without this nothing ever updated CurrentPlayers, recorded player
        // sessions or sent the Discord join/leave messages.
        _ = Task.Run(() => PollPlayersAsync(config, manager, backend, notify, new PlayerStatsService(config), stoppingToken), stoppingToken);
        _ = Task.Run(() => PeriodicUpdateLoopAsync(config, manager, backend, steamCmd, backup, notify, stoppingToken), stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }
    }

    private async Task PollPlayersAsync(ConfigService config, ServerManagerService manager, LocalServerBackend backend,
        NotificationService notify, PlayerStatsService stats, CancellationToken ct)
    {
        // Last known player keys per server; absent = not polled yet, so the first poll after a start (or after
        // WGS.ServiceHost starts with servers already running) records sessions but sends no join messages.
        var known = new Dictionary<string, Dictionary<string, (string Name, string SteamId)>>();
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(15_000, ct); } catch (OperationCanceledException) { return; }
            try
            {
                var runningIds = new HashSet<string>();
                foreach (var cfg in config.LoadServers())
                {
                    var inst = manager.GetInstance(cfg.Id);
                    if (inst == null || !manager.IsRunning(cfg.Id)) continue;
                    runningIds.Add(cfg.Id);
                    var server = inst.Server;   // the live object — CurrentPlayers/PlayerCountUnknown are set on it, not on a reloaded copy

                    var players = await backend.GetOnlinePlayersAsync(server);
                    if (server.PlayerCountUnknown) continue;   // failed query ≠ everybody left

                    // Not ToDictionary: nameless players ("?") can repeat a key
                    var curr = new Dictionary<string, (string Name, string SteamId)>();
                    foreach (var p in players) curr[p.SteamId.Length > 0 ? p.SteamId : p.Name] = (p.Name, p.SteamId);
                    var first = !known.TryGetValue(cfg.Id, out var prev);
                    prev ??= [];
                    foreach (var (key, p) in curr.Where(c => !prev.ContainsKey(c.Key)))
                    {
                        stats.RecordJoin(cfg.Id, p.Name, p.SteamId);
                        if (!first) _ = notify.NotifyPlayerEventAsync(server, p.Name, joined: true, curr.Count);
                    }
                    foreach (var (key, p) in prev.Where(c => !curr.ContainsKey(c.Key)))
                    {
                        stats.RecordLeave(cfg.Id, p.Name, p.SteamId);
                        _ = notify.NotifyPlayerEventAsync(server, p.Name, joined: false, curr.Count);
                    }
                    known[cfg.Id] = curr;
                }
                foreach (var gone in known.Keys.Where(k => !runningIds.Contains(k)).ToList()) known.Remove(gone);
            }
            catch (Exception ex) { logger.LogDebug(ex, "Player poll failed"); }
        }
    }

    /// <summary>"Auto-update periodically" for servers run by the service (the WPF window's own timer does not run in
    /// service mode). Same rules as the window: only when Steam has a newer build, optionally only when the server is
    /// empty, with an in-game warning and a minute's wait otherwise.</summary>
    private async Task PeriodicUpdateLoopAsync(ConfigService config, ServerManagerService manager, LocalServerBackend backend,
        SteamCmdService steamCmd, BackupService backup, NotificationService notify, CancellationToken ct)
    {
        var lastCheck = new Dictionary<string, DateTime>();
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
            try
            {
                foreach (var cfg in config.LoadServers().Where(x => x.AutoUpdate))
                {
                    var plugin = Games.GameRegistry.Get(cfg.GameId);
                    if (plugin == null || plugin.SteamAppId <= 0) continue;
                    var live = manager.GetServer(cfg.Id);
                    if (live == null || live.Status != ServerStatus.Running) { lastCheck.Remove(cfg.Id); continue; }

                    var interval = TimeSpan.FromMinutes(cfg.AutoUpdateIntervalMin > 0 ? cfg.AutoUpdateIntervalMin : 30);
                    // first check one interval after the server came up, like the window's timer
                    if (!lastCheck.TryGetValue(cfg.Id, out var last)) { lastCheck[cfg.Id] = DateTime.UtcNow; continue; }
                    if (DateTime.UtcNow - last < interval) continue;
                    lastCheck[cfg.Id] = DateTime.UtcNow;

                    await UpdateIfNewerAsync(cfg, live, plugin, manager, backend, steamCmd, backup, notify, config, ct);
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Periodic update check failed"); }
        }
    }

    private async Task UpdateIfNewerAsync(GameServer cfg, GameServer live, Games.IGamePlugin plugin, ServerManagerService manager,
        LocalServerBackend backend, SteamCmdService steamCmd, BackupService backup, NotificationService notify,
        ConfigService config, CancellationToken ct)
    {
        var branch = cfg.GameSpecificSettings.TryGetValue("steamBranch", out var b) && !string.IsNullOrWhiteSpace(b) ? b : plugin.SteamBranch;
        var installed = plugin.GetSteamInstalledBuildId(cfg);
        var latest    = await steamCmd.GetLatestBuildIdAsync(plugin.SteamAppId, branch, plugin.RequiresSteamLogin ? config.SteamLogin : null);
        if (latest == null || (installed != null && installed == latest)) return;

        bool Empty() => !live.PlayerCountUnknown && live.CurrentPlayers == 0;
        if (cfg.UpdateOnlyWhenEmpty && !Empty()) return;

        logger.LogInformation("Updating {Server}: build {Old} -> {New}", cfg.DisplayName, installed ?? "unknown", latest);
        if (!cfg.UpdateOnlyWhenEmpty)
        {
            await manager.WarnPlayersAsync(live, "Server restarting for an update in 1 minute");
            await Task.Delay(60_000, ct);
        }
        else if (!Empty()) return;   // someone joined meanwhile — next interval

        var wasAutoRestart = live.AutoRestart;
        try
        {
            live.AutoRestart = false;   // no crash-restart in the middle of stop -> update -> start
            await manager.StopAsync(live);
            if (cfg.BackupOnShutdown)
            {
                try { await backup.CreateBackupAsync(cfg); }
                catch (Exception ex) { logger.LogWarning(ex, "Backup after shutdown failed for {Server}", cfg.DisplayName); }
            }
            await backend.InstallAsync(FindFresh(config, cfg.Id) ?? cfg);
            live.AutoRestart = wasAutoRestart;

            var fresh = FindFresh(config, cfg.Id) ?? cfg;
            if (fresh.BackupOnStart)
            {
                try { await backup.CreateBackupAsync(fresh); }
                catch (Exception ex) { logger.LogWarning(ex, "Backup before start failed for {Server}", cfg.DisplayName); }
            }
            await manager.StartAsync(fresh);
            await notify.NotifyAsync($"🔄 {fresh.DisplayName} updated & restarted", plugin.GameName, "#58A6FF");
        }
        catch (Exception ex)
        {
            live.AutoRestart = wasAutoRestart;
            logger.LogWarning(ex, "Auto-update of {Server} failed", cfg.DisplayName);
        }
    }

    private static GameServer? FindFresh(ConfigService config, string id)
    {
        var s = config.LoadServers().FirstOrDefault(x => x.Id == id);
        if (s != null) s.Status = ServerStatus.Stopped;
        return s;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("WGS.ServiceHost stopping");
        _webApi?.Stop();
        _scheduler?.Dispose();
        _wake?.Dispose();
        _health?.Dispose();
        _network?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
