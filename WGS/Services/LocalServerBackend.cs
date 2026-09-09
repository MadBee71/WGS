using System.IO;
using System.IO.Compression;
using System.Net.Http;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// "All-in-one" mode implementation of <see cref="IServerBackend"/> — calls the exact same
/// Services/* classes ServerViewModel calls today, in-process, with no WPF/UI-thread code
/// (no Dispatcher, no ObservableCollection mutation, no OnPropertyChanged). The caller —
/// ServerViewModel, in a later integration step — is responsible for all of that, plus
/// anything that was previously a WpfMsgBox confirmation/guard in ServerViewModel: those are
/// UI concerns and are intentionally not reproduced here (see per-method notes below for the
/// handful of guards that carry real behavior and were kept, just turned into thrown
/// exceptions instead of a message box + silent return).
///
/// This class is not wired into the app yet (see IServerBackend.cs for the migration plan).
/// </summary>
public class LocalServerBackend : IServerBackend
{
    private readonly ServerManagerService _manager;
    private readonly SteamCmdService      _steamCmd;
    private readonly BackupService        _backup;
    private readonly NotificationService  _notifications;
    private readonly ConfigService        _config;
    private readonly ModManagerService    _mods;
    private readonly SourceModService     _sourceMod;
    private readonly ConfigEditorService  _configEditor;
    private readonly SteamWorkshopService _workshop;
    private readonly WorkshopDbService    _workshopDb;
    private readonly TemplateService      _templates;
    private readonly ScheduledTaskService _scheduler;
    private readonly ServerHygieneService _hygiene;
    private readonly ConfigPresetService  _presets;

    private static readonly HttpClient _manualDownloadHttp = new();

    public LocalServerBackend(ServerManagerService manager, SteamCmdService steamCmd, BackupService backup,
        NotificationService notifications, ConfigService config, ModManagerService mods,
        SourceModService sourceMod, ConfigEditorService configEditor, SteamWorkshopService workshop,
        WorkshopDbService workshopDb, TemplateService templates, ScheduledTaskService scheduler,
        ServerHygieneService hygiene, ConfigPresetService presets)
    {
        _manager       = manager;
        _steamCmd      = steamCmd;
        _backup        = backup;
        _notifications = notifications;
        _config        = config;
        _mods          = mods;
        _sourceMod     = sourceMod;
        _configEditor  = configEditor;
        _workshop      = workshop;
        _workshopDb    = workshopDb;
        _templates     = templates;
        _scheduler     = scheduler;
        _hygiene       = hygiene;
        _presets       = presets;
    }

    private GameServer? FindServer(string serverId) => _config.LoadServers().FirstOrDefault(s => s.Id == serverId);

    // ── Lifecycle ────────────────────────────────────────────────────────────
    // Mirrors ServerViewModel.StartAsync/StopAsync/KillAsync (ServerViewModel.cs:528-614), minus
    // AppendLog calls (no log channel on these three in IServerBackend) and minus perf-monitor /
    // update-timer start-stop (ServerViewModel-owned UI/polling state, not backend concerns).
    // Unlike the ViewModel versions, exceptions are NOT swallowed here — the ViewModel there
    // catches purely to turn failures into AppendLog lines; that's a UI concern the future
    // caller can still do around this call.

    public async Task StartAsync(GameServer server, IProgress<string>? log = null)
    {
        var plugin = GameRegistry.Get(server.GameId);

        if ((server.UpdateOnStart || server.AutoUpdate) && plugin?.SteamAppId > 0)
            await InstallAsync(server, log);

        if (server.BackupOnStart)
        {
            try
            {
                log?.Report("[WGS] Creating backup before start...");
                await _backup.CreateBackupAsync(server);
                log?.Report("[WGS] Backup created.");
            }
            catch (Exception ex) { log?.Report($"[WGS] Backup before start failed: {ex.Message}"); }
        }

        // Inject active Workshop mod IDs so plugins can build correct launch args
        if (plugin is IWorkshopPlugin && _workshop.SupportsWorkshop(plugin))
        {
            var ids = _workshopDb.GetModsForServer(server.Id)
                .Where(m => m.IsEnabled)
                .Select(m => $"@{m.ModId}")
                .ToList();
            server.GameSpecificSettings["__wgsWorkshopMods"] = string.Join(";", ids);
        }

        await _manager.StartAsync(server);
    }

    public async Task StopAsync(GameServer server, IProgress<string>? log = null)
    {
        await _manager.StopAsync(server);

        if (server.BackupOnShutdown)
        {
            try
            {
                log?.Report("[WGS] Creating backup after shutdown...");
                await _backup.CreateBackupAsync(server);
                log?.Report("[WGS] Backup created.");
            }
            catch (Exception ex) { log?.Report($"[WGS] Backup after shutdown failed: {ex.Message}"); }
        }
    }

    public async Task KillAsync(GameServer server) => await _manager.KillAsync(server);

    // ── Install / Update ────────────────────────────────────────────────────
    // Mirrors ServerViewModel.InstallAsync + InstallViaSteamCmdAsync + InstallFromManualDownloadAsync
    // (ServerViewModel.cs:630-833) as closely as possible. AppendLog(msg, type) calls become
    // log?.Report(msg) — the message text itself already carries the same "[WGS]"/"[ERR]"/"[Backup]"
    // prefixes the console view used to color-code by type, so nothing is lost but the color.
    // The FiveM/RedM build-channel picker keeps showing Views.BuildChannelDialog synchronously,
    // exactly as today — this backend only ever runs in-process with WPF (per task instructions).

    public async Task InstallAsync(GameServer server, IProgress<string>? log = null)
    {
        var plugin = GameRegistry.Get(server.GameId);
        if (plugin == null) return;

        var wasRunning = server.Status == ServerStatus.Running;
        if (wasRunning)
        {
            log?.Report("[WGS] Server is running — stopping before update...");
            await _manager.StopAsync(server);
        }

        try
        {
            if (plugin.SteamAppId <= 0)
                await InstallFromManualDownloadAsync(server, plugin, log);
            else
                await InstallViaSteamCmdAsync(server, plugin, log);
        }
        finally
        {
            if (wasRunning)
            {
                log?.Report("[WGS] Update finished — restarting server...");
                try { await _manager.StartAsync(server); }
                catch (Exception ex) { log?.Report($"[ERR] Failed to restart server after update: {ex.Message}"); }
            }
        }
    }

    private async Task InstallViaSteamCmdAsync(GameServer server, IGamePlugin plugin, IProgress<string>? log)
    {
        string? login = null, password = null;
        if (plugin.RequiresSteamLogin)
        {
            if (string.IsNullOrWhiteSpace(_config.SteamLogin) || string.IsNullOrWhiteSpace(_config.SteamPassword))
            {
                log?.Report("[WGS] ⚠ This game requires Steam login. Enter Steam username and password in the Settings page.");
                return;
            }
            login    = _config.SteamLogin;
            password = _config.SteamPassword;
        }

        server.Status = ServerStatus.Installing;
        log?.Report($"[WGS] {LocalizationService.Instance.InstallingText} {plugin.GameName}...");

        // NOTE: faithfully preserved from ServerViewModel.InstallViaSteamCmdAsync — this check
        // runs right after Status was just set to Installing above, so "!= NotInstalled" is
        // always true here. Reproduced as-is (not a bug I introduced, not mine to fix per the
        // "faithful extraction, not a rewrite" instruction).
        if (server.BackupEnabled && server.Status != ServerStatus.NotInstalled)
        {
            try
            {
                await _backup.CreateBackupAsync(server);
                log?.Report("[Backup] Auto-backup created before update.");
            }
            catch (Exception ex) { log?.Report($"[Backup] Pre-update backup failed: {ex.Message}"); }
        }

        try
        {
            var branch = server.GameSpecificSettings.TryGetValue("steamBranch", out var b) && !string.IsNullOrWhiteSpace(b)
                ? b : plugin.SteamBranch;
            await _steamCmd.InstallOrUpdateAsync(server.Id, plugin.SteamAppId, server.InstallPath, login, password, branch);
            var isUpdate = server.Status != ServerStatus.NotInstalled;
            if (isUpdate)
                await plugin.PostUpdateAsync(server, msg => log?.Report(msg));
            else
                await plugin.PostInstallAsync(server, msg => log?.Report(msg));
            server.Status = ServerStatus.Stopped;
            log?.Report("[WGS] " + LocalizationService.Instance.InstallDone);
            await _notifications.NotifyAsync($"✅ {server.DisplayName} {LocalizationService.Instance.InstallDone}", plugin.GameName, "#3FB950");
        }
        catch (FileNotFoundException ex)
        {
            log?.Report("[ERR] Server executable not found. Try clicking Install/Update again — if that doesn't help, check the Files tab to confirm the game actually downloaded, or check Settings → Install Path.");
            log?.Report("[ERR] " + ex.Message);
            server.Status = ServerStatus.Error;
        }
        catch (InvalidOperationException ex)
        {
            log?.Report("[ERR] " + ex.Message);
            server.Status = ServerStatus.Error;
        }
        catch (Exception ex)
        {
            log?.Report("[ERR] Unexpected error: " + ex.Message);
            server.Status = ServerStatus.Error;
        }
    }

    private async Task InstallFromManualDownloadAsync(GameServer server, IGamePlugin plugin, IProgress<string>? log)
    {
        server.Status = ServerStatus.Installing;
        try
        {
            Directory.CreateDirectory(server.InstallPath);

            if (plugin.HasHeavyInstall && _manager.RunningCount > 0)
                log?.Report("[WGS] ⚠ Warning: other servers are running. This install compiles code locally and may cause lag — consider stopping them first.");

            var handled = await plugin.TryCustomInstallAsync(server, msg => log?.Report(msg));
            if (handled)
            {
                server.Status = ServerStatus.Stopped;
                log?.Report($"[WGS] ✅ {LocalizationService.Instance.InstallDone}");
                await _notifications.NotifyAsync($"✅ {server.DisplayName} {LocalizationService.Instance.InstallDone}", plugin.GameName, "#3FB950");
                return;
            }
        }
        catch (Exception ex)
        {
            log?.Report("[ERR] Install failed: " + ex.Message);
            server.Status = ServerStatus.Error;
            return;
        }

        if (plugin.SupportsVersionCheck)
        {
            var (recommended, latest) = await plugin.GetAvailableBuildsAsync(server);
            var dlg = new Views.BuildChannelDialog(plugin.GameName, recommended, latest, () => plugin.GetAvailableBuildsAsync(server))
            { Owner = System.Windows.Application.Current?.MainWindow };
            dlg.ShowDialog();
            if (dlg.Result == Views.BuildChannelResult.Cancel)
            {
                server.Status = ServerStatus.Stopped;
                return;
            }
            server.GameSpecificSettings["buildChannel"] = dlg.Result == Views.BuildChannelResult.Latest ? "latest" : "recommended";
        }

        var info = await plugin.GetManualDownloadInfoAsync(server);
        if (info == null)
        {
            log?.Report($"[WGS] ⚠ {plugin.GameName} isn't distributed via Steam — install it manually, then point this server's Install Path at it. {plugin.Description}");
            return;
        }
        var (build, url) = info.Value;

        server.Status = ServerStatus.Installing;
        log?.Report($"[WGS] {LocalizationService.Instance.InstallingText} {plugin.GameName}...");

        // NOTE: same preserved-as-is quirk as InstallViaSteamCmdAsync — see comment there.
        if (server.BackupEnabled && server.Status != ServerStatus.NotInstalled)
        {
            try
            {
                await _backup.CreateBackupAsync(server);
                log?.Report("[Backup] Auto-backup created before update.");
            }
            catch (Exception ex) { log?.Report($"[Backup] Pre-update backup failed: {ex.Message}"); }
        }

        try
        {
            Directory.CreateDirectory(server.InstallPath);
            var zipPath = Path.Combine(server.InstallPath, "_wgs_download.zip");

            log?.Report($"[WGS] Downloading {url}...");
            var bytes = await _manualDownloadHttp.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(zipPath, bytes);

            log?.Report("[WGS] Extracting...");
            var execDir   = Path.GetDirectoryName(plugin.Executable);
            var targetDir = string.IsNullOrEmpty(execDir) ? server.InstallPath : Path.Combine(server.InstallPath, execDir);
            Directory.CreateDirectory(targetDir);
            ZipFile.ExtractToDirectory(zipPath, targetDir, overwriteFiles: true);
            File.Delete(zipPath);

            server.GameSpecificSettings["installedBuild"] = build;
            server.Status = ServerStatus.Stopped;
            log?.Report($"[WGS] {LocalizationService.Instance.InstallDone} (build {build})");
            await _notifications.NotifyAsync($"✅ {server.DisplayName} {LocalizationService.Instance.InstallDone}", $"{plugin.GameName} — build {build}", "#3FB950");
        }
        catch (Exception ex)
        {
            log?.Report("[ERR] Download/extract failed: " + ex.Message);
            server.Status = ServerStatus.Error;
        }
    }

    // These three exist only for the future remote backend, which has to expose the FiveM/RedM
    // build-channel choice as a poll-able "awaiting input" state because it can't show a WPF
    // dialog server-side. LocalServerBackend handles the choice inline (see BuildChannelDialog
    // above), so these are harmless no-ops here — per task instructions.
    public Task<bool> HasPendingBuildChannelChoiceAsync(string serverId) => Task.FromResult(false);
    public Task<(string recommended, string latest)?> GetBuildChannelOptionsAsync(string serverId) => Task.FromResult<(string, string)?>(null);
    public Task SubmitBuildChannelChoiceAsync(string serverId, bool useLatest) => Task.CompletedTask;

    // ── Console ─────────────────────────────────────────────────────────────
    // Mirrors ServerViewModel.RunCommandTextAsync's non-RCON branch (ServerViewModel.cs:878-881)
    // and MainViewModel's WebApiService.GetLog wiring (MainViewModel.cs:317-328).

    public async Task SendConsoleCommandAsync(string serverId, string command)
        => await _manager.SendCommandAsync(serverId, command);

    public Task<(List<string> lines, List<string> types, int nextOffset)> GetLogAsync(string serverId, int offset)
    {
        var inst = _manager.GetInstance(serverId);
        if (inst == null) return Task.FromResult((new List<string>(), new List<string>(), offset));

        var all   = inst.GetLogSnapshot();
        var slice = all.Skip(offset).ToList();
        return Task.FromResult((
            slice.Select(m => m.Text).ToList(),
            slice.Select(m => m.Type.ToString()).ToList(),
            offset + slice.Count
        ));
    }

    // ── Backups ──────────────────────────────────────────────────────────────
    // Mirrors ServerViewModel.cs:1029-1103. The "stop the server before restoring" guard
    // (ServerViewModel.cs:1077-1081, a WpfMsgBox-free AppendLog + return there) becomes a thrown
    // exception here since there's no UI to show an inline warning in.

    public async Task<BackupEntry> CreateBackupAsync(GameServer server) => await _backup.CreateBackupAsync(server);

    public Task<List<BackupEntry>> GetBackupsAsync(GameServer server) => Task.FromResult(_backup.GetBackupsForServer(server));

    /// <summary>
    /// FIX (caught in review): <paramref name="fileName"/> is a bare filename, not a full path —
    /// matching the established wire convention already used by MainViewModel's WebApiService
    /// wiring (MainViewModel.cs:352-365) and WebApiService's own path-traversal guard on the
    /// pre-existing restore route. BackupService.RestoreBackupAsync/DeleteBackup both expect a
    /// full path, so it must be resolved (and validated to stay inside the server's own backup
    /// directory) here first — passing the bare name straight through, as originally written,
    /// would silently fail with FileNotFoundException every time.
    /// </summary>
    private string ResolveBackupPath(GameServer server, string fileName)
    {
        var dir  = Path.GetFullPath(Path.Combine(_backup.BackupRoot, server.Id));
        var path = Path.GetFullPath(Path.Combine(dir, fileName));
        if (!path.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid backup path");
        return path;
    }

    public async Task RestoreBackupAsync(GameServer server, string fileName)
    {
        if (server.Status == ServerStatus.Running)
            throw new InvalidOperationException(LocalizationService.Instance.RestoreStopFirst);
        var path = ResolveBackupPath(server, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException("Backup file not found", path);
        await _backup.RestoreBackupAsync(server, path);
    }

    public Task DeleteBackupAsync(GameServer server, string fileName)
    {
        var path = ResolveBackupPath(server, fileName);
        _backup.DeleteBackup(path);
        return Task.CompletedTask;
    }

    public Task<List<BackupEntry>> CleanupBackupsAsync(GameServer server)
    {
        var toDelete = _backup.GetBackupsToDelete(server, server.BackupRetention);
        foreach (var b in toDelete)
            _backup.DeleteBackup(b.FilePath);
        return Task.FromResult(toDelete);
    }

    // ── Config file editor ──────────────────────────────────────────────────
    // Mirrors ServerViewModel.cs:1370-1426, backed by ConfigEditorService. FindConfigs' entries
    // are matched by Name (ConfigFileEntry.Name — either the bare filename for a plugin-declared
    // config, or the InstallPath-relative path for an auto-discovered one, same as ServerViewModel's
    // SelectedConfigFile selection today).

    public Task<List<ConfigFileEntry>> GetConfigFilesAsync(GameServer server)
    {
        var plugin = GameRegistry.Get(server.GameId);
        return Task.FromResult(_configEditor.FindConfigs(server, plugin));
    }

    public async Task<string> ReadConfigFileAsync(GameServer server, string fileName)
    {
        var configs = await GetConfigFilesAsync(server);
        var entry = configs.FirstOrDefault(c => c.Name == fileName)
            ?? throw new FileNotFoundException($"Config file not found: {fileName}");
        _configEditor.LoadContent(entry);
        return entry.Content;
    }

    public async Task SaveConfigFileAsync(GameServer server, string fileName, string content)
    {
        var configs = await GetConfigFilesAsync(server);
        var entry = configs.FirstOrDefault(c => c.Name == fileName)
            ?? throw new FileNotFoundException($"Config file not found: {fileName}");
        entry.Content = content;
        _configEditor.Save(server.Id, entry);
    }

    public async Task<List<ConfigSnapshot>> GetConfigSnapshotsAsync(GameServer server, string fileName)
    {
        var configs = await GetConfigFilesAsync(server);
        var entry = configs.FirstOrDefault(c => c.Name == fileName);
        return entry == null ? [] : _configEditor.GetHistory(server.Id, entry.Path);
    }

    /// <summary>
    /// JUDGMENT CALL: ConfigSnapshot only carries the .bak file's own path, not which config
    /// file it belongs to. ConfigEditorService's history folder is keyed by the config file's
    /// base name only (ConfigEditorService.HistoryDir uses Path.GetFileName(filePath) — see
    /// ConfigEditorService.cs:127-131), so the base name is recovered from the snapshot's parent
    /// directory name and matched back to one of this server's discovered config files. In
    /// ServerViewModel this worked implicitly because SelectedConfigFile was already the right
    /// entry (set by whatever the user had open in the editor) — that context doesn't exist at
    /// this layer, so it's reconstructed instead.
    /// </summary>
    public async Task RestoreConfigSnapshotAsync(GameServer server, ConfigSnapshot snapshot)
    {
        var baseName = Path.GetFileName(Path.GetDirectoryName(snapshot.FilePath) ?? "");
        var configs  = await GetConfigFilesAsync(server);
        var entry = configs.FirstOrDefault(c => Path.GetFileName(c.Path) == baseName)
            ?? throw new FileNotFoundException($"Could not resolve the original config file for snapshot: {snapshot.FilePath}");

        var content = _configEditor.ReadSnapshot(snapshot.FilePath);
        entry.Content = content;
        _configEditor.Save(server.Id, entry);
    }

    // ── Config presets ───────────────────────────────────────────────────────

    public Task<string?> ApplyPresetAsync(GameServer server, ConfigPreset preset)
        => Task.FromResult(_presets.ApplyPreset(server, preset));

    // ── Mods ─────────────────────────────────────────────────────────────────
    // Mirrors ServerViewModel.cs:1224-1360 (InstallOxideAsync..InstallVanillaAsync), dispatching
    // on the `framework` string values named in the interface doc comment.

    public async Task InstallModFrameworkAsync(GameServer server, string framework, IProgress<(int pct, string msg)> progress)
    {
        var plugin = GameRegistry.Get(server.GameId);
        switch (framework)
        {
            case "oxide":
                if (plugin == null || !plugin.SupportsOxide) return;
                await _mods.InstallOxideAsync(plugin, server.InstallPath, progress);
                break;
            case "paper":
                if (plugin?.MinecraftFlavor != "paper") return;
                await _mods.InstallPaperAsync(server.InstallPath, progress);
                break;
            case "spigot":
                await _mods.InstallSpigotAsync(server.InstallPath, progress);
                break;
            case "purpur":
                await _mods.InstallPurpurAsync(server.InstallPath, progress);
                break;
            case "fabric":
                await _mods.InstallFabricAsync(server.InstallPath, progress);
                break;
            case "forge":
                await _mods.InstallForgeAsync(server.InstallPath, progress);
                break;
            case "vanilla":
                await _mods.InstallVanillaAsync(server.InstallPath, progress);
                break;
            default:
                throw new ArgumentException($"Unknown mod framework: {framework}", nameof(framework));
        }
    }

    // ── SourceMod ────────────────────────────────────────────────────────────
    // Mirrors ServerViewModel.cs:222-258.

    public Task<(List<SourceModPlugin> active, List<SourceModPlugin> disabled)> GetSourceModPluginsAsync(GameServer server)
    {
        var active   = _sourceMod.GetActivePlugins(server.InstallPath);
        var disabled = _sourceMod.GetDisabledPlugins(server.InstallPath);
        return Task.FromResult((active, disabled));
    }

    public Task SetSourceModPluginEnabledAsync(GameServer server, string fileName, bool enabled)
    {
        if (enabled) _sourceMod.EnablePlugin(server.InstallPath, fileName);
        else _sourceMod.DisablePlugin(server.InstallPath, fileName);
        return Task.CompletedTask;
    }

    // ── Steam Workshop ───────────────────────────────────────────────────────
    // Mirrors ServerViewModel.cs:1640-1772. GetWorkshopModsAsync returns the WorkshopDbService-
    // backed list (WorkshopDbMods in the ViewModel) — not SteamWorkshopService.GetInstalledItemsAsync's
    // List<WorkshopItem>, which is a different type representing on-disk state and isn't part of
    // this interface's List<WorkshopMod> shape.

    public Task<List<WorkshopMod>> GetWorkshopModsAsync(GameServer server)
        => Task.FromResult(_workshopDb.GetModsForServer(server.Id));

    public async Task<List<WorkshopItem>> SearchWorkshopAsync(GameServer server, string query)
    {
        var plugin = GameRegistry.Get(server.GameId);
        return plugin == null ? [] : await _workshop.SearchWorkshopAsync(plugin, query);
    }

    public async Task InstallWorkshopItemAsync(GameServer server, ulong itemId, IProgress<(int pct, string msg)>? progress = null)
    {
        var plugin = GameRegistry.Get(server.GameId);
        if (plugin == null) return;
        await _workshop.InstallItemAsync(server, plugin, itemId, progress);
    }

    public async Task UninstallWorkshopItemAsync(GameServer server, WorkshopMod mod)
    {
        var plugin = GameRegistry.Get(server.GameId);
        if (plugin == null) return;
        await _workshop.UninstallItemAsync(server, plugin, mod.ModId);
    }

    public Task SetWorkshopModEnabledAsync(GameServer server, WorkshopMod mod, bool enabled)
    {
        // WorkshopMod doesn't implement INPC — same manual toggle ServerViewModel.ToggleModEnabled does.
        mod.IsEnabled = enabled;
        _workshopDb.SetEnabled(server.Id, mod.ModId, enabled);
        return Task.CompletedTask;
    }

    public async Task<List<WorkshopMod>> CheckOutdatedModsAsync(GameServer server)
        => await _workshop.CheckForOutdatedModsAsync(server);

    public async Task UpdateModsAsync(GameServer server, IEnumerable<WorkshopMod>? onlyThese = null, IProgress<(int pct, string msg)>? progress = null)
    {
        var plugin = GameRegistry.Get(server.GameId);
        if (plugin == null) return;

        if (onlyThese == null)
        {
            await _workshop.UpdateAllModsAsync(server, plugin, progress);
        }
        else
        {
            foreach (var mod in onlyThese)
                await _workshop.InstallItemAsync(server, plugin, mod.ModId, progress);
        }
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────
    // Mirrors ServerViewModel.cs:1805-1837. The "server must be stopped" guard
    // (ServerViewModel.cs:1808-1813, a WpfMsgBox there) becomes a thrown exception here.

    public Task<List<string>> CleanupJunkFilesAsync(GameServer server)
    {
        if (server.Status == ServerStatus.Running)
            throw new InvalidOperationException("Stop the server first — junk cleanup only scans/deletes files while it's not running.");

        var junk = _hygiene.ScanJunk(server);
        _hygiene.DeleteJunk(junk);
        return Task.FromResult(junk.Select(j => j.Path).ToList());
    }

    // ── Templates ────────────────────────────────────────────────────────────
    // Mirrors ServerViewModel.cs:1983-2023.

    public Task SaveAsTemplateAsync(GameServer server, string name, string description, string category, List<string> tags)
    {
        _templates.SaveFromServer(server, name, description: description, category: category, tags: tags);
        return Task.CompletedTask;
    }

    public Task ApplyTemplateAsync(GameServer server, ServerTemplate template)
    {
        _templates.ApplyToServer(template, server);
        return Task.CompletedTask;
    }

    public Task<ServerTemplate> CloneTemplateAsync(ServerTemplate template) => Task.FromResult(_templates.Clone(template.Id));

    public Task DeleteTemplateAsync(ServerTemplate template)
    {
        _templates.Delete(template.Id);
        return Task.CompletedTask;
    }

    // ── Scheduled tasks ──────────────────────────────────────────────────────
    // Mirrors ServerViewModel.cs:2108-2128.
    //
    // JUDGMENT CALL: AddScheduledTaskAsync's interface signature takes only the task, not the
    // server (unlike ServerViewModel.AddScheduledTask, which stamps task.ServerId/ServerName from
    // Server before calling _scheduler.AddTask). The caller is expected to have set those fields
    // on the task already — this just persists it as given.

    public Task<List<ScheduledTask>> GetScheduledTasksAsync(string serverId)
        => Task.FromResult(_scheduler.Tasks.Where(t => t.ServerId == serverId).ToList());

    public Task AddScheduledTaskAsync(ScheduledTask task)
    {
        _scheduler.AddTask(task);
        return Task.CompletedTask;
    }

    public Task RemoveScheduledTaskAsync(ScheduledTask task)
    {
        _scheduler.RemoveTask(task.Id);
        return Task.CompletedTask;
    }

    // ── Quick commands / log-watch rules ─────────────────────────────────────
    // Mirrors ServerViewModel.cs:905-961 — plain list mutation on the GameServer model only;
    // servers.json persistence already happens elsewhere (MainViewModel.Save()), not duplicated here.

    public Task AddQuickCommandAsync(GameServer server, QuickCommand qc)
    {
        server.QuickCommands.Add(qc);
        return Task.CompletedTask;
    }

    public Task RemoveQuickCommandAsync(GameServer server, QuickCommand qc)
    {
        server.QuickCommands.Remove(qc);
        return Task.CompletedTask;
    }

    public Task AddLogWatchRuleAsync(GameServer server, LogWatchRule rule)
    {
        server.LogWatchRules.Add(rule);
        return Task.CompletedTask;
    }

    public Task RemoveLogWatchRuleAsync(GameServer server, LogWatchRule rule)
    {
        server.LogWatchRules.Remove(rule);
        return Task.CompletedTask;
    }

    // ── Players ──────────────────────────────────────────────────────────────
    // Mirrors the non-RCON branches of ServerViewModel.FetchOnlinePlayersAsync
    // (ServerViewModel.cs:1468-1564): IRestPlayersPlugin (REST API), IA2SQueryPlugin (A2S UDP
    // query) and the Minecraft SLP fallback. Per the interface's scope note, RCON-capable games
    // query players directly client-side over their own RCON socket instead of going through
    // this backend, so the RCON branches (which need ServerViewModel's live RconService instance)
    // are intentionally not reproduced — a plugin that only exposes GetPlayersCommand() (RCON)
    // and nothing else returns an empty list here. PlayerStatsService session recording and join/
    // leave Discord notifications are ServerViewModel-owned side effects of that same method and
    // are left there, not duplicated here — this method only fetches the current list.

    public async Task<List<OnlinePlayer>> GetOnlinePlayersAsync(GameServer server)
    {
        var plugin = GameRegistry.Get(server.GameId);
        if (plugin == null) return [];

        List<OnlinePlayer> parsed;

        if (plugin is IRestPlayersPlugin restPlugin)
        {
            parsed = await restPlugin.GetPlayersAsync(server);
        }
        else if (plugin is IA2SQueryPlugin a2sPlugin)
        {
            parsed = await A2SQueryService.QueryPlayersAsync(a2sPlugin.A2SHost, a2sPlugin.GetA2SPort(server));
        }
        else if (plugin is MinecraftPluginBase)
        {
            var slp = await MinecraftSLPService.QueryAsync("127.0.0.1", server.ServerPort);
            if (slp == null) return [];
            parsed = Enumerable.Range(0, slp.Value.Online).Select(_ => new OnlinePlayer { Name = "?" }).ToList();
        }
        else
        {
            return [];
        }

        // Keep the model in sync — same as ServerViewModel.FetchOnlinePlayersAsync.
        server.CurrentPlayers = parsed.Count;
        return parsed;
    }
}
