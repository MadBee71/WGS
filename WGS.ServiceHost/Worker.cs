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

        GameServer? FindServer(string id) => config.LoadServers().FirstOrDefault(s => s.Id == id);

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

        var webApi = new WebApiService { Users = users, DashboardEnabled = true };
        _webApi = webApi;

        // ── Pre-existing delegate surface ───────────────────────────────────────────────────
        webApi.GetServers    = () => config.LoadServers();
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

        webApi.GetOnlinePlayers = id => backend.GetOnlinePlayersAsync(FindServer(id)!).GetAwaiter().GetResult();

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

        var serverCount = config.LoadServers().Count;
        logger.LogInformation("servers.json: {Path} ({Count} server(s))", config.ServersFile, serverCount);
        logger.LogInformation("Listening on http://localhost:{Port}/ (all interfaces: {AllInterfaces})", Port, webApi.BoundToAllInterfaces);
        Console.WriteLine("WGS.ServiceHost running.");
        Console.WriteLine($"servers.json: {config.ServersFile}  ({serverCount} server(s))");
        Console.WriteLine($"Listening on http://localhost:{Port}/  (all interfaces: {webApi.BoundToAllInterfaces})");
        Console.WriteLine($"Token: {config.WebApiToken}");
        Console.WriteLine("(This token is saved in settings.json — point a WPF client's Settings -> Service mode at this URL and token.)");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("WGS.ServiceHost stopping");
        _webApi?.Stop();
        _scheduler?.Dispose();
        _network?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
