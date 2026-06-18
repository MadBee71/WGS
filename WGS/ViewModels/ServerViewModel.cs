using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WGS.Games;
using WGS.Models;
using WGS.Services;

namespace WGS.ViewModels;

public partial class ServerViewModel : BaseViewModel, IDisposable
{
    private readonly ServerManagerService  _manager;
    private readonly SteamCmdService       _steamCmd;
    private readonly BackupService         _backup;
    private readonly NotificationService   _notifications;
    private readonly PerformanceMonitorService _perfMonitor;
    private readonly ConfigService         _config;
    private readonly ModManagerService     _mods;
    private readonly ConfigEditorService   _configEditor;
    private readonly PlayerStatsService    _playerStats;
    private readonly PerfHistoryService    _perfHistory;
    private readonly SteamWorkshopService  _workshop;
    private readonly WorkshopDbService     _workshopDb;
    private readonly TemplateService        _templates;
    private readonly ScheduledTaskService   _scheduler;
    private readonly NetworkMonitorService  _network;
    private RconService? _rcon;
    private readonly SemaphoreSlim _rconLock  = new(1, 1);
    private readonly object        _perfLock  = new();
    private System.Timers.Timer?   _updateTimer;

    public GameServer Server { get; }
    public IGamePlugin? Plugin { get; }
    public ObservableCollection<ConsoleMessage> Log { get; } = [];
    public ObservableCollection<BackupEntry> Backups { get; } = [];
    public ObservableCollection<string> ActionLog { get; } = [];

    [ObservableProperty] private int _serverNumber;

    [ObservableProperty] private string _consoleInput = string.Empty;
    [ObservableProperty] private int _installProgress;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private long _memoryMb;
    [ObservableProperty] private bool _rconConnected;
    [ObservableProperty] private string _consoleFilter   = string.Empty;
    [ObservableProperty] private string _modStatusText   = string.Empty;
    [ObservableProperty] private bool   _modBusy;

    // Config editor
    [ObservableProperty] private List<Services.ConfigFileEntry> _configFiles = [];
    [ObservableProperty] private Services.ConfigFileEntry? _selectedConfigFile;
    [ObservableProperty] private string _configContent = string.Empty;

    // Players — reaaliaikainen lista
    [ObservableProperty] private List<Models.OnlinePlayer>    _onlinePlayers  = [];
    [ObservableProperty] private List<Services.PlayerSession> _playerHistory  = [];
    [ObservableProperty] private List<Services.PlayerStats>   _playerStatsList = [];
    [ObservableProperty] private bool   _playersBusy;
    [ObservableProperty] private string _kickReason  = string.Empty;
    [ObservableProperty] private string _banReason   = string.Empty;

    private System.Timers.Timer? _playerRefreshTimer;

    // Performance history
    [ObservableProperty] private OxyPlot.PlotModel _perfPlot = CreateEmptyPlot();
    private OxyPlot.PlotModel?          _perfModel;
    private OxyPlot.Series.LineSeries?  _cpuSeries;
    private OxyPlot.Series.LineSeries?  _memSeries;
    [ObservableProperty] private int _perfRangeMinutes = 15;
    partial void OnPerfRangeMinutesChanged(int _) => UpdatePerfChart();
    public IReadOnlyList<int> PerfRangeOptions { get; } = [5, 15, 30, 60];

    // Workshop
    [ObservableProperty] private List<Services.WorkshopItem>   _workshopItems = [];
    [ObservableProperty] private List<Services.WorkshopMod>    _workshopDbMods = [];
    [ObservableProperty] private List<Services.WorkshopItem>   _workshopSearchResults = [];
    [ObservableProperty] private string _workshopItemId      = string.Empty;
    [ObservableProperty] private string _workshopSearchQuery = string.Empty;
    [ObservableProperty] private bool   _workshopBusy;
    [ObservableProperty] private string _workshopItemIdPreview = string.Empty;
    private System.Timers.Timer? _workshopIdLookupTimer;

    partial void OnWorkshopItemIdChanged(string value)
    {
        _workshopIdLookupTimer?.Stop();
        WorkshopItemIdPreview = string.Empty;
        if (!ulong.TryParse(value, out var id) || id == 0) return;

        _workshopIdLookupTimer ??= new System.Timers.Timer(500) { AutoReset = false };
        _workshopIdLookupTimer.Elapsed -= WorkshopIdLookupElapsed;
        _workshopIdLookupTimer.Elapsed += WorkshopIdLookupElapsed;
        _workshopIdLookupTimer.Start();

        void WorkshopIdLookupElapsed(object? _, System.Timers.ElapsedEventArgs __) => _ = LookupWorkshopItemNameAsync(id);
    }

    private async Task LookupWorkshopItemNameAsync(ulong id)
    {
        var title = await Services.SteamWorkshopService.TryGetItemTitleAsync(id);
        if (ulong.TryParse(WorkshopItemId, out var current) && current != id) return; // stale lookup, ID changed since
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
            WorkshopItemIdPreview = title ?? "(not found)");
    }

    // Scheduled tasks
    [ObservableProperty] private List<Services.ScheduledTask> _scheduledTasks = [];

    // Resource limits
    [ObservableProperty] private long _maxRamMb;
    partial void OnMaxRamMbChanged(long value) => Server.MaxRamMb = value;

    // Backup retention
    [ObservableProperty] private int _backupRetention;
    partial void OnBackupRetentionChanged(int value) => Server.BackupRetention = value;
    [ObservableProperty] private int _backupMaxAgeDays;
    partial void OnBackupMaxAgeDaysChanged(int value) => Server.BackupMaxAgeDays = value;

    public bool HasWorkshop => _workshop.SupportsWorkshop(Plugin);
    public bool HasPlayerCommands => Plugin?.GetPlayersCommand() != null
                                  || Plugin?.GetKickCommand("") != null;

    public FileBrowserViewModel FileBrowser { get; } = new();
    public List<Models.ServerTemplate> GameTemplates => FilteredTemplates;

    // ── Template filter ───────────────────────────────────────────────────────
    [ObservableProperty] private string _templateFilterCategory = string.Empty;
    [ObservableProperty] private string _templateFilterTag      = string.Empty;

    partial void OnTemplateFilterCategoryChanged(string _) => OnPropertyChanged(nameof(FilteredTemplates));
    partial void OnTemplateFilterTagChanged(string _)      => OnPropertyChanged(nameof(FilteredTemplates));

    public List<Models.ServerTemplate> FilteredTemplates
    {
        get
        {
            var all = _templates.ForGame(Server.GameId);
            if (!string.IsNullOrWhiteSpace(TemplateFilterCategory))
                all = all.Where(t => t.Category.Equals(TemplateFilterCategory,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(TemplateFilterTag))
                all = all.Where(t => t.Tags.Contains(TemplateFilterTag,
                    StringComparer.OrdinalIgnoreCase)).ToList();
            return all.ToList();
        }
    }

    public List<string> AvailableCategories =>
        _templates.ForGame(Server.GameId)
                  .Select(t => t.Category)
                  .Where(c => !string.IsNullOrWhiteSpace(c))
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .Prepend("(all)")
                  .ToList();

    private System.Timers.Timer? _perfTimer;

    public bool HasModSupport       => Plugin?.SupportsOxide == true
                                    || !string.IsNullOrEmpty(Plugin?.MinecraftFlavor);
    public bool HasMinecraftSupport => !string.IsNullOrEmpty(Plugin?.MinecraftFlavor);

    public List<CpuCoreItem> CpuCores { get; }
    public string[] PriorityOptions { get; } = ["Normal", "AboveNormal", "High", "BelowNormal", "RealTime"];

    // ── Batch selection ───────────────────────────────────────────────────────
    [ObservableProperty] private bool _isBatchSelected;
    internal Action? BatchSelectionChanged { get; set; }
    partial void OnIsBatchSelectedChanged(bool _) => BatchSelectionChanged?.Invoke();

    public string StatusColor => Server.Status switch
    {
        ServerStatus.Running      => "#3FB950",
        ServerStatus.Starting     => "#58A6FF",
        ServerStatus.Stopping     => "#58A6FF",
        ServerStatus.Stopped      => "#8B949E",
        ServerStatus.Installing   => "#58A6FF",
        ServerStatus.Updating     => "#58A6FF",
        ServerStatus.Error        => "#F85149",
        ServerStatus.NotInstalled => "#8B949E",
        _                         => "#8B949E",
    };

    public bool IsRunning    => Server.Status == ServerStatus.Running;
    public bool IsStopped    => Server.Status is ServerStatus.Stopped or ServerStatus.NotInstalled;
    public bool CanStart     => Server.Status is ServerStatus.Stopped or ServerStatus.Error or ServerStatus.NotInstalled;
    public bool CanStop      => Server.Status is ServerStatus.Running or ServerStatus.Starting;
    public bool HasRcon          => Plugin?.HasRcon == true;
    public bool UseNativeConsole => Plugin?.UseNativeConsole == true;

    public string? GameImageUrl => Plugin?.GameStoreAppId > 0
        ? $"https://cdn.akamai.steamstatic.com/steam/apps/{Plugin.GameStoreAppId}/capsule_sm_120.jpg"
        : null;
    public string UptimeText => _manager.GetInstance(Server.Id)?.Uptime is TimeSpan t && t > TimeSpan.Zero
        ? $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
        : "--:--:--";

    public string DailyRestartTimeText
    {
        get => Server.DailyRestartTime.ToString(@"hh\:mm");
        set
        {
            if (TimeSpan.TryParseExact(value, @"hh\:mm", null, out var ts))
                Server.DailyRestartTime = ts;
        }
    }

    // ── Kaistaseuranta ────────────────────────────────────────────────────────

    [ObservableProperty] private string _netIn          = "—";
    [ObservableProperty] private string _netOut         = "—";
    [ObservableProperty] private int    _connectionCount = 0;
    [ObservableProperty] private IReadOnlyList<double> _netInHistory  = [];
    [ObservableProperty] private IReadOnlyList<double> _netOutHistory = [];

    public bool HasNetworkStats => _network.GetServerStats(Server.Id) != null;

    private void OnServerStatsUpdated(string serverId)
    {
        if (serverId != Server.Id) return;
        var stats = _network.GetServerStats(serverId);
        if (stats == null) return;

        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            NetIn           = NetworkMonitorService.FormatSpeed(stats.BytesInPerSec);
            NetOut          = NetworkMonitorService.FormatSpeed(stats.BytesOutPerSec);
            ConnectionCount = stats.ConnectionCount;
            NetInHistory    = stats.HistoryIn.ToList();
            NetOutHistory   = stats.HistoryOut.ToList();
        });
    }

    public ObservableCollection<ConsoleMessage> FilteredLog
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ConsoleFilter)) return Log;
            var filter = ConsoleFilter.ToLowerInvariant();
            return new ObservableCollection<ConsoleMessage>(
                Log.Where(m => m.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public ServerViewModel(GameServer server, ServerManagerService manager, SteamCmdService steamCmd,
        BackupService backup, NotificationService notifications, PerformanceMonitorService perfMonitor,
        ConfigService config, ModManagerService mods,
        ConfigEditorService configEditor, PlayerStatsService playerStats,
        PerfHistoryService perfHistory, SteamWorkshopService workshop, WorkshopDbService workshopDb,
        TemplateService templates, ScheduledTaskService scheduler, NetworkMonitorService network)
    {
        Server         = server;
        Plugin         = GameRegistry.Get(server.GameId);
        _manager       = manager;
        _steamCmd      = steamCmd;
        _backup        = backup;
        _notifications = notifications;
        _perfMonitor   = perfMonitor;
        _config        = config;
        _mods          = mods;
        _configEditor  = configEditor;
        _playerStats   = playerStats;
        _perfHistory   = perfHistory;
        _workshop      = workshop;
        _workshopDb    = workshopDb;
        _templates     = templates;
        _scheduler     = scheduler;
        _network       = network;

        _network.ServerStatsUpdated += OnServerStatsUpdated;
        FileBrowser.Initialize(server.InstallPath);

        _manager.LogReceived      += OnLogReceived;
        _manager.StatusChanged    += OnStatusChanged;
        _manager.CrashLimitReached += OnCrashLimitReached;
        _manager.PortsReassigned  += OnPortsReassigned;
        _steamCmd.OutputReceived  += OnSteamOutput;
        _steamCmd.ProgressChanged += OnSteamProgress;

        // Build CPU core checkboxes
        var coreCount = Environment.ProcessorCount;
        CpuCores = Enumerable.Range(0, coreCount).Select(i =>
        {
            var bit = 1L << i;
            var enabled = Server.CpuAffinityMask == 0 || (Server.CpuAffinityMask & bit) != 0;
            return new CpuCoreItem(i, enabled);
        }).ToList();

        foreach (var item in CpuCores)
        {
            item.Changed += () =>
            {
                long mask = 0;
                foreach (var c in CpuCores)
                    if (c.IsEnabled) mask |= (1L << c.Index);
                // If all cores selected, store 0 (= no restriction)
                Server.CpuAffinityMask = mask == ((1L << coreCount) - 1) ? 0 : mask;
            };
        }

        _maxRamMb = Server.MaxRamMb; // initialize without triggering OnMaxRamMbChanged
        _backupRetention   = Server.BackupRetention;
        _backupMaxAgeDays  = Server.BackupMaxAgeDays;

        PluginFields = Plugin?.GetConfigFields()
            .Where(f => f.Key is not ("serverName" or "maxPlayers" or "serverPass"))
            .Select(f => new PluginFieldVm(server, f))
            .ToList() ?? [];

        RefreshStatus();
        RefreshBackups();

        // If the server was already running when this ViewModel was created (e.g. reattached
        // to a process that survived a WGS restart), the StatusChanged(Running) event already
        // fired before we subscribed to it. Start monitoring directly in that case.
        if (IsRunning)
        {
            StartPerfMonitoring();
            StartPlayerRefresh();
            StartUpdateTimer();
        }
    }

    // ── Start / Stop / Restart ──────────────────────────────────────────────

    [RelayCommand]
    private async Task StartAsync()
    {
        try
        {
            if ((Server.UpdateOnStart || Server.AutoUpdate) && Plugin?.SteamAppId > 0)
                await InstallAsync();

            if (Server.BackupOnStart)
            {
                try
                {
                    AppendLog("[WGS] Creating backup before start...", ConsoleMessageType.System);
                    await _backup.CreateBackupAsync(Server);
                    AppendLog("[WGS] Backup created.", ConsoleMessageType.System);
                }
                catch (Exception ex) { AppendLog($"[WGS] Backup before start failed: {ex.Message}", ConsoleMessageType.Warning); }
            }

            // Inject active Workshop mod IDs so plugins can build correct launch args
            if (Plugin is Games.IWorkshopPlugin && HasWorkshop)
            {
                var ids = _workshopDb.GetModsForServer(Server.Id)
                    .Where(m => m.IsEnabled)
                    .Select(m => $"@{m.ModId}")
                    .ToList();
                Server.GameSpecificSettings["__wgsWorkshopMods"] = string.Join(";", ids);
            }

            await _manager.StartAsync(Server);
            // StartPerfMonitoring() and StartUpdateTimer() are called from OnStatusChanged(Running)
        }
        catch (FileNotFoundException ex)
        {
            AppendLog("[ERR] Executable not found after install. Check the game plugin's Executable field.", ConsoleMessageType.Error);
            AppendLog("[ERR] " + ex.Message, ConsoleMessageType.Error);
        }
        catch (InvalidOperationException ex)
        {
            AppendLog("[ERR] " + ex.Message, ConsoleMessageType.Error);
        }
        catch (Exception ex)
        {
            AppendLog("[ERR] Unexpected error: " + ex.Message, ConsoleMessageType.Error);
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        try
        {
            StopUpdateTimer();
            await _manager.StopAsync(Server);
            // StopPerfMonitoring() called from OnStatusChanged(Stopped)
        }
        catch (Exception ex) { AppendLog("[ERR] " + ex.Message, ConsoleMessageType.Error); }
    }

    [RelayCommand]
    private async Task KillAsync()
    {
        try
        {
            await _manager.KillAsync(Server);
            AppendLog("[WGS] Process killed.", ConsoleMessageType.System);
            StopPerfMonitoring();
        }
        catch (Exception ex) { AppendLog("[ERR] " + ex.Message, ConsoleMessageType.Error); }
    }

    [RelayCommand]
    private void ShowWindow() => _manager.ShowWindow(Server);

    [RelayCommand]
    private async Task RestartAsync()
    {
        AppendLog("[WGS] " + Loc.StatusStopping, ConsoleMessageType.System);
        await StopAsync();
        await Task.Delay(3000);
        await StartAsync();
    }

    // ── Install / Update ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (Plugin == null) return;

        string? login = null, password = null;
        if (Plugin.RequiresSteamLogin)
        {
            if (string.IsNullOrWhiteSpace(_config.SteamLogin) || string.IsNullOrWhiteSpace(_config.SteamPassword))
            {
                AppendLog("[WGS] ⚠ This game requires Steam login. Enter Steam username and password in the Settings page.", ConsoleMessageType.Warning);
                return;
            }
            login    = _config.SteamLogin;
            password = _config.SteamPassword;
        }

        IsInstalling      = true;
        Server.Status     = ServerStatus.Installing;
        RefreshStatus();
        AppendLog($"[WGS] {Loc.InstallingText} {Plugin.GameName}...", ConsoleMessageType.System);

        // Auto-backup before update (only when there is something to back up)
        if (Server.BackupEnabled && Server.Status != ServerStatus.NotInstalled)
        {
            try { await _backup.CreateBackupAsync(Server); AppendLog("[Backup] Auto-backup created before update.", ConsoleMessageType.System); }
            catch (Exception ex) { AppendLog($"[Backup] Pre-update backup failed: {ex.Message}", ConsoleMessageType.Warning); }
        }

        try
        {
            await _steamCmd.InstallOrUpdateAsync(Server.Id, Plugin.SteamAppId, Server.InstallPath, login, password, Plugin.SteamBranch);
            Server.Status = ServerStatus.Stopped;
            AppendLog("[WGS] " + Loc.InstallDone, ConsoleMessageType.System);
            await _notifications.NotifyAsync($"✅ {Server.DisplayName} {Loc.InstallDone}", Plugin.GameName, "#3FB950");
        }
        catch (FileNotFoundException ex)
        {
            AppendLog("[ERR] Executable not found after install. Check the game plugin's Executable field.", ConsoleMessageType.Error);
            AppendLog("[ERR] " + ex.Message, ConsoleMessageType.Error);
            Server.Status = ServerStatus.Error;
        }
        catch (InvalidOperationException ex)
        {
            AppendLog("[ERR] " + ex.Message, ConsoleMessageType.Error);
            Server.Status = ServerStatus.Error;
        }
        catch (Exception ex)
        {
            AppendLog("[ERR] Unexpected error: " + ex.Message, ConsoleMessageType.Error);
            Server.Status = ServerStatus.Error;
        }
        finally { IsInstalling = false; RefreshStatus(); }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        AppendLog("[WGS] " + Loc.StatusUpdating, ConsoleMessageType.System);
        await InstallAsync();
    }

    // ── Console ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SendConsoleCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(ConsoleInput)) return;
        var cmd = ConsoleInput;
        ConsoleInput = string.Empty;
        AppendLog("> " + cmd, ConsoleMessageType.Input);

        if (RconConnected)
        {
            await _rconLock.WaitAsync();
            try
            {
                if (_rcon != null)
                {
                    var resp = await _rcon.SendCommandAsync(cmd);
                    if (!string.IsNullOrEmpty(resp))
                        AppendLog(resp, ConsoleMessageType.Info);
                }
            }
            finally { _rconLock.Release(); }
        }
        else
        {
            _manager.SendCommand(Server.Id, cmd);
        }
    }

    [RelayCommand]
    private void ClearConsole() => Log.Clear();

    partial void OnConsoleFilterChanged(string value) => OnPropertyChanged(nameof(FilteredLog));

    // ── RCON ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ConnectRconAsync()
    {
        // Swap in a fresh RconService under lock so SendConsoleCommandAsync never sees a half-constructed state
        RconService newRcon;
        await _rconLock.WaitAsync();
        try
        {
            _rcon?.Dispose();
            _rcon = new RconService();
            newRcon = _rcon;
        }
        finally { _rconLock.Release(); }

        var ip   = string.IsNullOrEmpty(Server.ServerIp) || Server.ServerIp == "0.0.0.0" ? "127.0.0.1" : Server.ServerIp;
        var port = Server.RconPort > 0 ? Server.RconPort : Server.ServerPort + 10;
        var ok   = await newRcon.ConnectAsync(ip, port, Server.RconPassword);

        RconConnected = ok;
        AppendLog(ok ? "[RCON] " + Loc.RconConnectedMsg : "[RCON] " + Loc.RconFailedMsg,
            ok ? ConsoleMessageType.System : ConsoleMessageType.Error);
    }

    [RelayCommand]
    private void DisconnectRcon()
    {
        _rconLock.Wait();
        try
        {
            _rcon?.Dispose();
            _rcon = null;
        }
        finally { _rconLock.Release(); }

        RconConnected = false;
        AppendLog("[RCON] " + Loc.RconDisconnectedMsg, ConsoleMessageType.System);
    }

    // ── Backups ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        try
        {
            AppendLog("[Backup] " + Loc.BackupCreating, ConsoleMessageType.System);
            var entry = await _backup.CreateBackupAsync(Server);
            AppendLog($"[Backup] {Loc.BackupDone}: {entry.SizeText}", ConsoleMessageType.System);
            await _notifications.NotifyAsync($"💾 {Server.DisplayName} {Loc.BackupDone}", entry.SizeText, "#D29922");
            RefreshBackups();
        }
        catch (Exception ex) { AppendLog("[ERR] " + ex.Message, ConsoleMessageType.Error); }
    }

    [RelayCommand]
    private void CleanupBackupsNow()
    {
        var toDelete = _backup.GetBackupsToDelete(Server, Server.BackupRetention);
        if (toDelete.Count == 0)
        {
            WpfMsgBox.Show("No backups currently match the retention/age limits — nothing to delete.",
                "Clean up backups", WpfMsgBoxButton.OK, WpfMsgBoxImage.Information);
            return;
        }

        var totalSize = toDelete.Sum(b => b.SizeBytes);
        var sizeText  = totalSize >= 1024 * 1024 * 1024
            ? $"{totalSize / (1024.0 * 1024 * 1024):F2} GB"
            : $"{totalSize / (1024.0 * 1024):F1} MB";

        var result = WpfMsgBox.Show(
            $"This will permanently delete {toDelete.Count} backup(s) totaling {sizeText}, based on the current " +
            $"\"Keep at most {Server.BackupRetention}\" / \"older than {Server.BackupMaxAgeDays} days\" settings.\n\n" +
            "This cannot be undone. Continue?",
            "Clean up backups", WpfMsgBoxButton.YesNo, WpfMsgBoxImage.Warning);
        if (result != WpfMsgBoxResult.Yes) return;

        foreach (var b in toDelete)
            _backup.DeleteBackup(b.FilePath);

        AppendLog($"[Backup] Cleaned up {toDelete.Count} old backup(s) ({sizeText}).", ConsoleMessageType.System);
        RefreshBackups();
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(BackupEntry? entry)
    {
        if (entry == null) return;
        if (IsRunning)
        {
            AppendLog("[ERR] " + Loc.RestoreStopFirst, ConsoleMessageType.Error);
            return;
        }
        AppendLog($"[Restore] {Loc.RestoreStarting} {entry.CreatedAt:dd.MM.yyyy HH:mm}...", ConsoleMessageType.System);
        await _backup.RestoreBackupAsync(Server, entry.FilePath);
        AppendLog("[Restore] " + Loc.RestoreDone, ConsoleMessageType.System);
    }

    [RelayCommand]
    private void DeleteBackup(BackupEntry? entry)
    {
        if (entry == null) return;
        _backup.DeleteBackup(entry.FilePath);
        RefreshBackups();
    }

    public void RefreshBackups()
    {
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            Backups.Clear();
            foreach (var b in _backup.GetBackupsForServer(Server))
                Backups.Add(b);
        });
    }

    // ── Port check ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CheckPortsAsync()
    {
        AppendLog("[Ports] " + Loc.PortChecking, ConsoleMessageType.System);
        var results = PortCheckerService.CheckServerPorts(Server);
        foreach (var r in results)
            AppendLog($"[Ports] {r.Message}", r.IsAvailable ? ConsoleMessageType.System : ConsoleMessageType.Warning);

        var extIp = await PortCheckerService.GetExternalIpAsync();
        if (extIp != null)
            AppendLog($"[Ports] {Loc.ExternalIp}: {extIp}", ConsoleMessageType.System);
    }

    // ── Open folder ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenInstallFolder()
    {
        if (System.IO.Directory.Exists(Server.InstallPath))
            System.Diagnostics.Process.Start("explorer.exe", Server.InstallPath);
    }

    // ── Auto Update timer ────────────────────────────────────────────────────

    private void StartUpdateTimer()
    {
        StopUpdateTimer();
        if (!Server.AutoUpdate || Plugin?.SteamAppId == 0) return;

        int intervalMin = Server.AutoUpdateIntervalMin > 0 ? Server.AutoUpdateIntervalMin : 30;
        _updateTimer = new System.Timers.Timer(intervalMin * 60_000);
        _updateTimer.Elapsed += async (_, _) => await RunPeriodicUpdateAsync();
        _updateTimer.AutoReset = true;
        _updateTimer.Start();
        AppendLog($"[AutoUpdate] Scheduled — checking every {intervalMin} min", ConsoleMessageType.System);
    }

    private void StopUpdateTimer()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _updateTimer = null;
    }

    private async Task RunPeriodicUpdateAsync()
    {
        if (!IsRunning) return; // server was stopped manually
        AppendLog("[AutoUpdate] Checking for updates...", ConsoleMessageType.System);

        bool wasAutoRestart = Server.AutoRestart;
        try
        {
            // Temporarily disable AutoRestart so we don't get an auto-restart
            // during the stop → update → start cycle
            Server.AutoRestart = false;
            await _manager.WarnPlayersAsync(Server, "Server restarting for an update in 1 minute");
            await Task.Delay(60_000);
            await _manager.StopAsync(Server);
            // StopPerfMonitoring called by OnStatusChanged

            await InstallAsync(); // runs SteamCMD update

            if (!Server.AutoUpdate)
            {
                Server.AutoRestart = wasAutoRestart;
                return;
            }
            Server.AutoRestart = wasAutoRestart;

            await _manager.StartAsync(Server);
            // StartPerfMonitoring + StartUpdateTimer called by OnStatusChanged(Running)
            AppendLog("[AutoUpdate] ✅ Updated and restarted.", ConsoleMessageType.System);
            await _notifications.NotifyAsync($"🔄 {Server.DisplayName} updated & restarted", Plugin?.GameName ?? "", "#58A6FF");
        }
        catch (Exception ex)
        {
            Server.AutoRestart = wasAutoRestart;
            AppendLog($"[AutoUpdate] ❌ {ex.Message}", ConsoleMessageType.Error);
        }
    }

    // ── Mod manager ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task InstallOxideAsync()
    {
        if (Plugin == null || !Plugin.SupportsOxide) return;
        ModBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"[{x.pct}%] {x.msg}"));

            await _mods.InstallOxideAsync(Plugin, Server.InstallPath, progress);
            AppendLog("[Mods] ✅ Oxide installed successfully.", ConsoleMessageType.System);
        }
        catch (Exception ex)
        {
            AppendLog($"[Mods] ❌ {ex.Message}", ConsoleMessageType.Error);
            WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"❌ {ex.Message}");
        }
        finally { ModBusy = false; }
    }

    [RelayCommand]
    private async Task InstallPaperAsync()
    {
        if (Plugin?.MinecraftFlavor != "paper") return;
        ModBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"[{x.pct}%] {x.msg}"));

            await _mods.InstallPaperAsync(Server.InstallPath, progress);
            AppendLog("[Mods] ✅ Paper installed successfully.", ConsoleMessageType.System);
        }
        catch (Exception ex)
        {
            AppendLog($"[Mods] ❌ {ex.Message}", ConsoleMessageType.Error);
            WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"❌ {ex.Message}");
        }
        finally { ModBusy = false; }
    }

    [RelayCommand]
    private async Task InstallSpigotAsync()
    {
        ModBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"[{x.pct}%] {x.msg}"));
            await _mods.InstallSpigotAsync(Server.InstallPath, progress);
            AppendLog("[Mods] ✅ Spigot compiled and installed.", ConsoleMessageType.System);
        }
        catch (Exception ex)
        {
            AppendLog($"[Mods] ❌ {ex.Message}", ConsoleMessageType.Error);
            WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"❌ {ex.Message}");
        }
        finally { ModBusy = false; }
    }

    [RelayCommand]
    private async Task InstallPurpurAsync()
    {
        ModBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"[{x.pct}%] {x.msg}"));
            await _mods.InstallPurpurAsync(Server.InstallPath, progress);
            AppendLog("[Mods] ✅ Purpur installed successfully.", ConsoleMessageType.System);
        }
        catch (Exception ex)
        {
            AppendLog($"[Mods] ❌ {ex.Message}", ConsoleMessageType.Error);
            WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"❌ {ex.Message}");
        }
        finally { ModBusy = false; }
    }

    [RelayCommand]
    private async Task InstallFabricAsync()
    {
        ModBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"[{x.pct}%] {x.msg}"));
            await _mods.InstallFabricAsync(Server.InstallPath, progress);
            AppendLog("[Mods] ✅ Fabric installed successfully.", ConsoleMessageType.System);
        }
        catch (Exception ex)
        {
            AppendLog($"[Mods] ❌ {ex.Message}", ConsoleMessageType.Error);
            WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"❌ {ex.Message}");
        }
        finally { ModBusy = false; }
    }

    [RelayCommand]
    private async Task InstallForgeAsync()
    {
        ModBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"[{x.pct}%] {x.msg}"));
            await _mods.InstallForgeAsync(Server.InstallPath, progress);
            AppendLog("[Mods] ✅ Forge installed successfully.", ConsoleMessageType.System);
        }
        catch (Exception ex)
        {
            AppendLog($"[Mods] ❌ {ex.Message}", ConsoleMessageType.Error);
            WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"❌ {ex.Message}");
        }
        finally { ModBusy = false; }
    }

    [RelayCommand]
    private async Task InstallVanillaAsync()
    {
        ModBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"[{x.pct}%] {x.msg}"));
            await _mods.InstallVanillaAsync(Server.InstallPath, progress);
            AppendLog("[Mods] ✅ Vanilla server installed successfully.", ConsoleMessageType.System);
        }
        catch (Exception ex)
        {
            AppendLog($"[Mods] ❌ {ex.Message}", ConsoleMessageType.Error);
            WpfApplication.Current?.Dispatcher?.Invoke(() => ModStatusText = $"❌ {ex.Message}");
        }
        finally { ModBusy = false; }
    }

    [RelayCommand]
    private void OpenPluginFolder()
    {
        if (Plugin == null) return;
        ModManagerService.OpenPluginFolder(Plugin, Server.InstallPath);
    }

    // ── Config editor ───────────────────────────────────────────────────────

    [RelayCommand]
    private void LoadConfigFiles()
    {
        ConfigFiles = _configEditor.FindConfigs(Server, Plugin);
        if (ConfigFiles.Count > 0) SelectedConfigFile = ConfigFiles[0];
    }

    [RelayCommand]
    private void SaveConfigFile()
    {
        if (SelectedConfigFile == null) return;
        SelectedConfigFile.Content = ConfigContent;
        try { _configEditor.Save(SelectedConfigFile); AppendLog("[Config] File saved.", ConsoleMessageType.System); }
        catch (Exception ex) { AppendLog($"[Config] Save failed: {ex.Message}", ConsoleMessageType.Error); }
    }

    partial void OnSelectedConfigFileChanged(Services.ConfigFileEntry? value)
    {
        if (value == null) return;
        _configEditor.LoadContent(value);
        ConfigContent = value.Content;
    }

    // ── Players ──────────────────────────────────────────────────────────────

    private bool _playersFetchedOnce;

    private void StartPlayerRefresh()
    {
        _playerRefreshTimer?.Dispose();
        _playerRefreshTimer = new System.Timers.Timer(15_000);
        _playerRefreshTimer.Elapsed  += (_, _) => _ = FetchOnlinePlayersAsync();
        _playerRefreshTimer.AutoReset = true;
        _playerRefreshTimer.Start();
        _ = FetchOnlinePlayersAsync(); // ensimmäinen heti
    }

    private void StopPlayerRefresh()
    {
        _playerRefreshTimer?.Stop();
        _playerRefreshTimer?.Dispose();
        _playerRefreshTimer = null;
    }

    [RelayCommand]
    private async Task RefreshPlayersAsync()
    {
        await FetchOnlinePlayersAsync();
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            PlayerHistory = _playerStats.GetSessions(Server.Id, 50);
            PlayerStatsList = _playerStats.GetPlayerStats(Server.Id, 50);
        });
    }

    private async Task FetchOnlinePlayersAsync()
    {
        if (Plugin == null) return;

        List<Models.OnlinePlayer> parsed;

        if (Plugin is Games.IRestPlayersPlugin restPlugin)
        {
            parsed = await restPlugin.GetPlayersAsync(Server);
            if (restPlugin.LastRestApiError != null)
                AppendLog($"[REST API] {restPlugin.LastRestApiError}", ConsoleMessageType.Warning);
        }
        else
        {
            var cmd = Plugin.GetPlayersCommand();
            if (cmd == null) return;

            string response;
            if (RconConnected && _rcon != null)
            {
                await _rconLock.WaitAsync();
                try   { response = await _rcon.SendCommandAsync(cmd); }
                catch { return; }
                finally { _rconLock.Release(); }
            }
            else return;

            if (string.IsNullOrWhiteSpace(response)) return;
            parsed = Services.PlayerParserService.Parse(Plugin.EngineFamily, response);
        }

        // Keep the model in sync — used by Shut-down-when-empty and the web dashboard's
        // server list (the detail endpoint already gets a live count separately).
        Server.CurrentPlayers = parsed.Count;

        // Compare against the previous list → session logging
        var prev = OnlinePlayers.ToList();
        var currNames  = parsed.Select(p => p.SteamId.Length > 0 ? p.SteamId : p.Name).ToHashSet();
        var prevNames  = prev.Select(p => p.SteamId.Length > 0 ? p.SteamId : p.Name).ToHashSet();

        var joined = parsed.Where(p => !prevNames.Contains(p.SteamId.Length > 0 ? p.SteamId : p.Name)).ToList();
        var left   = prev.Where(p => !currNames.Contains(p.SteamId.Length > 0 ? p.SteamId : p.Name)).ToList();

        foreach (var p in joined)
            _playerStats.RecordJoin(Server.Id, p.Name, p.SteamId);

        foreach (var p in left)
            _playerStats.RecordLeave(Server.Id, p.Name, p.SteamId);

        // Skip Discord alerts on the very first fetch after opening a server, otherwise
        // every already-connected player would fire a spurious "joined" notification.
        if (_playersFetchedOnce)
        {
            foreach (var p in joined) _ = _notifications.NotifyPlayerEventAsync(Server, p.Name, joined: true, parsed.Count);
            foreach (var p in left)   _ = _notifications.NotifyPlayerEventAsync(Server, p.Name, joined: false, parsed.Count);
        }
        _playersFetchedOnce = true;

        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            OnlinePlayers = parsed;
            PlayerHistory = _playerStats.GetSessions(Server.Id, 50);
            PlayerStatsList = _playerStats.GetPlayerStats(Server.Id, 50);
        });
    }

    // ── Performance history ──────────────────────────────────────────────────

    private void EnsurePerfModel()
    {
        if (_perfModel != null) return;
        _cpuSeries = new OxyPlot.Series.LineSeries
        {
            Title = "CPU %", Color = OxyPlot.OxyColor.Parse("#58A6FF"),
            StrokeThickness = 2, MarkerType = OxyPlot.MarkerType.None,
        };
        _memSeries = new OxyPlot.Series.LineSeries
        {
            Title = "RAM MB", Color = OxyPlot.OxyColor.Parse("#3FB950"),
            StrokeThickness = 2, MarkerType = OxyPlot.MarkerType.None, YAxisKey = "mem",
        };
        _perfModel = new OxyPlot.PlotModel
        {
            Background          = OxyPlot.OxyColor.FromArgb(0, 0, 0, 0),
            PlotAreaBorderColor = OxyPlot.OxyColor.Parse("#30363d"),
            TextColor           = OxyPlot.OxyColor.Parse("#8b949e"),
        };
        _perfModel.Axes.Add(new OxyPlot.Axes.DateTimeAxis
        {
            Position = OxyPlot.Axes.AxisPosition.Bottom, StringFormat = "HH:mm:ss",
            AxislineColor = OxyPlot.OxyColor.Parse("#30363d"), TicklineColor = OxyPlot.OxyColor.Parse("#30363d"),
            MajorGridlineColor = OxyPlot.OxyColor.Parse("#21262d"), MajorGridlineStyle = OxyPlot.LineStyle.Solid,
        });
        _perfModel.Axes.Add(new OxyPlot.Axes.LinearAxis
        {
            Position = OxyPlot.Axes.AxisPosition.Left, Title = "CPU %", Minimum = 0, Maximum = 100,
            MajorGridlineColor = OxyPlot.OxyColor.Parse("#21262d"), MajorGridlineStyle = OxyPlot.LineStyle.Solid,
        });
        _perfModel.Axes.Add(new OxyPlot.Axes.LinearAxis
        {
            Key = "mem", Position = OxyPlot.Axes.AxisPosition.Right, Title = "RAM MB", Minimum = 0,
            MaximumPadding = 0.15, // headroom so peak usage doesn't look pinned to the top edge
        });
        _perfModel.Series.Add(_cpuSeries);
        _perfModel.Series.Add(_memSeries);
        WpfApplication.Current?.Dispatcher?.Invoke(() => PerfPlot = _perfModel);
    }

    private void UpdatePerfChart()
    {
        var cutoff  = DateTime.Now.AddMinutes(-PerfRangeMinutes);
        var samples = _perfHistory.Get(Server.Id).Where(s => s.Time >= cutoff).ToList();
        EnsurePerfModel();

        // Mutate series + invalidate on the UI thread together â€” this can be called from the
        // 2s timer's background thread, and OxyPlot's renderer reads Points on the UI thread.
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            _cpuSeries!.Points.Clear();
            _memSeries!.Points.Clear();
            foreach (var s in samples)
            {
                var x = OxyPlot.Axes.DateTimeAxis.ToDouble(s.Time);
                _cpuSeries.Points.Add(new OxyPlot.DataPoint(x, s.Cpu));
                _memSeries.Points.Add(new OxyPlot.DataPoint(x, s.MemMb));
            }
            _perfModel!.InvalidatePlot(true);
        });
    }

    private static OxyPlot.PlotModel CreateEmptyPlot()
    {
        var m = new OxyPlot.PlotModel { Background = OxyPlot.OxyColor.FromArgb(0, 0, 0, 0) };
        m.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Left });
        m.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Bottom });
        return m;
    }

    // ── Workshop ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshWorkshopAsync()
    {
        if (Plugin == null || !HasWorkshop) return;
        WorkshopItems  = await _workshop.GetInstalledItemsAsync(Server, Plugin);
        WorkshopDbMods = _workshopDb.GetModsForServer(Server.Id);
    }

    [RelayCommand]
    private async Task SearchWorkshopAsync()
    {
        if (Plugin == null || !HasWorkshop || string.IsNullOrWhiteSpace(WorkshopSearchQuery)) return;
        WorkshopBusy = true;
        try
        {
            WorkshopSearchResults = await _workshop.SearchWorkshopAsync(Plugin, WorkshopSearchQuery);
        }
        catch (Exception ex) { AppendLog($"[Workshop] Search failed: {ex.Message}", ConsoleMessageType.Error); }
        finally { WorkshopBusy = false; }
    }

    [RelayCommand]
    private async Task InstallWorkshopItemAsync()
    {
        if (!ulong.TryParse(WorkshopItemId, out var id)) { AppendLog("[Workshop] Invalid item ID.", ConsoleMessageType.Warning); return; }
        if (Plugin == null || !HasWorkshop) return;
        await InstallWorkshopByIdAsync(id);
        WorkshopItemId = string.Empty;
    }

    [RelayCommand]
    private async Task InstallWorkshopFromSearchAsync(WorkshopItem? item)
    {
        if (item == null || Plugin == null) return;
        await InstallWorkshopByIdAsync(item.PublishedFileId);
    }

    private async Task InstallWorkshopByIdAsync(ulong id)
    {
        if (Plugin == null || !HasWorkshop) return;
        WorkshopBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => AppendLog($"[Workshop] [{x.pct}%] {x.msg}", ConsoleMessageType.System)));
            await _workshop.InstallItemAsync(Server, Plugin, id, progress);
            AppendLog($"[Workshop] ✅ Item {id} installed.", ConsoleMessageType.System);
            await RefreshWorkshopAsync();
        }
        catch (Exception ex) { AppendLog($"[Workshop] ❌ {ex.Message}", ConsoleMessageType.Error); }
        finally { WorkshopBusy = false; }
    }

    [RelayCommand]
    private async Task UninstallWorkshopItemAsync(WorkshopMod? mod)
    {
        if (mod == null || Plugin == null) return;
        WorkshopBusy = true;
        try
        {
            await _workshop.UninstallItemAsync(Server, Plugin, mod.ModId);
            AppendLog($"[Workshop] Removed {mod.ModName}.", ConsoleMessageType.System);
            await RefreshWorkshopAsync();
        }
        catch (Exception ex) { AppendLog($"[Workshop] ❌ {ex.Message}", ConsoleMessageType.Error); }
        finally { WorkshopBusy = false; }
    }

    [RelayCommand]
    private void ToggleModEnabled(WorkshopMod? mod)
    {
        if (mod == null) return;
        // WorkshopMod doesn't implement INPC so the CheckBox two-way binding
        // doesn't update mod.IsEnabled before this fires — toggle it manually.
        mod.IsEnabled = !mod.IsEnabled;
        _workshopDb.SetEnabled(Server.Id, mod.ModId, mod.IsEnabled);
    }

    [RelayCommand]
    private async Task UpdateAllModsAsync()
    {
        if (Plugin == null || !HasWorkshop) return;
        WorkshopBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => AppendLog($"[Workshop] [{x.pct}%] {x.msg}", ConsoleMessageType.System)));
            await _workshop.UpdateAllModsAsync(Server, Plugin, progress);
            AppendLog("[Workshop] ✅ All mods updated.", ConsoleMessageType.System);
            await RefreshWorkshopAsync();
        }
        catch (Exception ex) { AppendLog($"[Workshop] ❌ {ex.Message}", ConsoleMessageType.Error); }
        finally { WorkshopBusy = false; }
    }

    // ── Direct Connect ────────────────────────────────────────────────────────

    [RelayCommand]
    private void JoinServer()
    {
        var ip   = string.IsNullOrEmpty(Server.ServerIp) || Server.ServerIp == "0.0.0.0" ? "127.0.0.1" : Server.ServerIp;
        var port = Server.QueryPort > 0 ? Server.QueryPort : Server.ServerPort;
        var uri  = $"steam://connect/{ip}:{port}";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex) { AppendLog($"[Connect] {ex.Message}", ConsoleMessageType.Error); }
    }

    [RelayCommand]
    private void CopyConnectionLink()
    {
        var ip   = string.IsNullOrEmpty(Server.ServerIp) || Server.ServerIp == "0.0.0.0" ? "127.0.0.1" : Server.ServerIp;
        var port = Server.QueryPort > 0 ? Server.QueryPort : Server.ServerPort;
        var link = $"steam://connect/{ip}:{port}";
        try
        {
            System.Windows.Clipboard.SetText(link);
            AppendLog($"[Connect] Copied: {link}", ConsoleMessageType.System);
        }
        catch (Exception ex) { AppendLog($"[Connect] {ex.Message}", ConsoleMessageType.Error); }
    }

    // ── Player Management ─────────────────────────────────────────────────────

    // Vanhanmallinen tekstisyöttö (yhteensopivuus)
    [ObservableProperty] private string _playerInput = string.Empty;

    [RelayCommand]
    private async Task KickPlayerAsync()
    {
        if (string.IsNullOrWhiteSpace(PlayerInput) || Plugin == null) return;
        var cmd = Plugin.GetKickCommand(PlayerInput,
            string.IsNullOrWhiteSpace(KickReason) ? "Kicked by admin" : KickReason);
        if (cmd == null) { AppendLog("[Players] Kick not supported.", ConsoleMessageType.Warning); return; }
        await SendRconOrConsole(cmd);
        AppendLog($"[Players] Kicked: {PlayerInput}", ConsoleMessageType.System);
        KickReason = string.Empty;
        _ = FetchOnlinePlayersAsync();
    }

    [RelayCommand]
    private async Task BanPlayerAsync()
    {
        if (string.IsNullOrWhiteSpace(PlayerInput) || Plugin == null) return;
        var cmd = Plugin.GetBanCommand(PlayerInput,
            string.IsNullOrWhiteSpace(BanReason) ? "Banned by admin" : BanReason);
        if (cmd == null) { AppendLog("[Players] Ban not supported.", ConsoleMessageType.Warning); return; }
        await SendRconOrConsole(cmd);
        AppendLog($"[Players] Banned: {PlayerInput}", ConsoleMessageType.System);
        BanReason = string.Empty;
        _ = FetchOnlinePlayersAsync();
    }

    // Kick/Ban suoraan pelaajan kortista
    [RelayCommand]
    private async Task KickOnlinePlayerAsync(Models.OnlinePlayer? player)
    {
        if (player == null || Plugin == null) return;
        var target = player.SteamId.Length > 0 ? player.SteamId : player.Name;
        var reason  = string.IsNullOrWhiteSpace(KickReason) ? "Kicked by admin" : KickReason;
        var cmd     = Plugin.GetKickCommand(target, reason);
        if (cmd == null) { AppendLog("[Players] Kick not supported.", ConsoleMessageType.Warning); return; }
        await SendRconOrConsole(cmd);
        AppendLog($"[Players] Kicked {player.Name} ({reason})", ConsoleMessageType.System);
        KickReason = string.Empty;
        _ = FetchOnlinePlayersAsync();
    }

    [RelayCommand]
    private async Task BanOnlinePlayerAsync(Models.OnlinePlayer? player)
    {
        if (player == null || Plugin == null) return;
        var target = player.SteamId.Length > 0 ? player.SteamId : player.Name;
        var reason  = string.IsNullOrWhiteSpace(BanReason) ? "Banned by admin" : BanReason;
        var cmd     = Plugin.GetBanCommand(target, reason);
        if (cmd == null) { AppendLog("[Players] Ban not supported.", ConsoleMessageType.Warning); return; }
        await SendRconOrConsole(cmd);
        AppendLog($"[Players] Banned {player.Name} ({reason})", ConsoleMessageType.System);
        BanReason = string.Empty;
        _ = FetchOnlinePlayersAsync();
    }

    [RelayCommand]
    private async Task ListPlayersAsync()
    {
        if (Plugin == null) return;
        var cmd = Plugin.GetPlayersCommand();
        if (cmd == null) { AppendLog("[Players] Player list not supported.", ConsoleMessageType.Warning); return; }
        await SendRconOrConsole(cmd);
    }

    private async Task SendRconOrConsole(string cmd)
    {
        if (RconConnected && _rcon != null)
        {
            await _rconLock.WaitAsync();
            try { var r = await _rcon.SendCommandAsync(cmd); if (!string.IsNullOrEmpty(r)) AppendLog(r); }
            finally { _rconLock.Release(); }
        }
        else
            _manager.SendCommand(Server.Id, cmd);
    }

    // ── Templates ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _templateName        = string.Empty;
    [ObservableProperty] private string _templateCategory    = string.Empty;
    [ObservableProperty] private string _templateTagsInput   = string.Empty;  // pilkkueroteltu
    [ObservableProperty] private string _templateDescription = string.Empty;

    [RelayCommand]
    private void SaveAsTemplate()
    {
        if (string.IsNullOrWhiteSpace(TemplateName)) return;
        var tags = TemplateTagsInput
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        _templates.SaveFromServer(Server, TemplateName, TemplateDescription, TemplateCategory, tags);
        var saved = TemplateName;
        TemplateName        = string.Empty;
        TemplateCategory    = string.Empty;
        TemplateTagsInput   = string.Empty;
        TemplateDescription = string.Empty;
        RefreshTemplates();
        AppendLog($"[Template] Saved as template \"{saved}\".", ConsoleMessageType.System);
    }

    [RelayCommand]
    private void ApplyTemplate(Models.ServerTemplate? template)
    {
        if (template == null) return;
        _templates.ApplyToServer(template, Server);
        AppendLog($"[Template] Applied \"{template.Name}\".", ConsoleMessageType.System);
    }

    [RelayCommand]
    private void CloneTemplate(Models.ServerTemplate? template)
    {
        if (template == null) return;
        var clone = _templates.Clone(template.Id);
        RefreshTemplates();
        AppendLog($"[Template] Cloned as \"{clone.Name}\".", ConsoleMessageType.System);
    }

    [RelayCommand]
    private void DeleteTemplate(Models.ServerTemplate? template)
    {
        if (template == null) return;
        _templates.Delete(template.Id);
        RefreshTemplates();
    }

    [RelayCommand]
    private void ExportTemplate(Models.ServerTemplate? template)
    {
        if (template == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Export template",
            Filter     = "JSON file|*.json",
            FileName   = SanitizeFileName(template.Name) + ".json",
            DefaultExt = ".json",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _templates.ExportSingle(template.Id, dlg.FileName);
            AppendLog($"[Template] Exported: {dlg.FileName}", ConsoleMessageType.System);
        }
        catch (Exception ex)
        {
            AppendLog($"[Template] Export failed: {ex.Message}", ConsoleMessageType.Error);
        }
    }

    [RelayCommand]
    private void ImportTemplates()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title     = "Import templates",
            Filter    = "JSON file|*.json",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        int total = 0;
        foreach (var file in dlg.FileNames)
        {
            try   { total += _templates.Import(file); }
            catch (Exception ex)
            {
                AppendLog($"[Template] Import failed ({Path.GetFileName(file)}): {ex.Message}",
                    ConsoleMessageType.Error);
            }
        }
        if (total > 0)
        {
            RefreshTemplates();
            AppendLog($"[Template] Imported {total} template(s).", ConsoleMessageType.System);
        }
    }

    private void RefreshTemplates()
    {
        OnPropertyChanged(nameof(GameTemplates));
        OnPropertyChanged(nameof(FilteredTemplates));
        OnPropertyChanged(nameof(AvailableCategories));
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    // ── Scheduled tasks ──────────────────────────────────────────────────────

    [RelayCommand]
    private void RefreshScheduledTasks()
        => ScheduledTasks = _scheduler.Tasks.Where(t => t.ServerId == Server.Id).ToList();

    [RelayCommand]
    private void AddScheduledTask(Services.ScheduledTask? task)
    {
        if (task == null) return;
        task.ServerId   = Server.Id;
        task.ServerName = Server.DisplayName;
        _scheduler.AddTask(task);
        RefreshScheduledTasks();
    }

    [RelayCommand]
    private void RemoveScheduledTask(Services.ScheduledTask? task)
    {
        if (task == null) return;
        _scheduler.RemoveTask(task.Id);
        RefreshScheduledTasks();
    }

    // ── Performance monitoring ───────────────────────────────────────────────

    private void StartPerfMonitoring()
    {
        var inst = _manager.GetInstance(Server.Id);
        if (inst?.Process == null) return;
        _perfMonitor.Track(Server.Id, inst.Process.Id);

        lock (_perfLock)
        {
            _perfTimer?.Stop();
            _perfTimer?.Dispose();
            _perfTimer = new System.Timers.Timer(2000);
            _perfTimer.Elapsed += (_, _) =>
            {
                lock (_perfLock)
                {
                    if (_perfTimer == null) return; // stopped between fire and lock
                }
                var m = _perfMonitor.Get(Server.Id);
                if (m == null) return;
                _perfHistory.Record(Server.Id, m.CurrentCpu, m.CurrentMemMb);
                UpdatePerfChart();
                WpfApplication.Current?.Dispatcher?.Invoke(() =>
                {
                    CpuPercent = Math.Round(m.CurrentCpu, 1);
                    MemoryMb   = m.CurrentMemMb;
                    OnPropertyChanged(nameof(UptimeText));
                });
            };
            _perfTimer.Start();
        }
    }

    /// <summary>
    /// Stops live monitoring for this ViewModel. Does NOT clear recorded history â€” this
    /// runs whenever the ViewModel is disposed (e.g. switching servers, closing WGS), which
    /// must not erase history for a server that's still actually running.
    /// </summary>
    private void StopPerfMonitoring()
    {
        lock (_perfLock)
        {
            _perfTimer?.Stop();
            _perfTimer?.Dispose();
            _perfTimer = null;
        }
        _perfMonitor.Untrack(Server.Id);
        _perfModel  = null;
        _cpuSeries  = null;
        _memSeries  = null;
        WpfApplication.Current?.Dispatcher?.Invoke(() => { CpuPercent = 0; MemoryMb = 0; PerfPlot = CreateEmptyPlot(); });
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private void OnLogReceived(string serverId, ConsoleMessage msg)
    {
        if (serverId != Server.Id) return;
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            Log.Add(msg);
            if (!string.IsNullOrWhiteSpace(ConsoleFilter))
                OnPropertyChanged(nameof(FilteredLog));
        });
    }

    private void OnStatusChanged(string serverId, ServerStatus status)
    {
        if (serverId != Server.Id) return;
        WpfApplication.Current?.Dispatcher?.Invoke(RefreshStatus);
        _ = _notifications.NotifyServerStatusAsync(Server, status);

        var actionText = status switch
        {
            ServerStatus.Running  => "Server started",
            ServerStatus.Stopped  => "Server stopped",
            ServerStatus.Error    => "Server crashed",
            ServerStatus.Updating => "Server updating",
            ServerStatus.Stopping => "Server stopping",
            _ => null
        };
        if (actionText != null) AddActionLog(actionText);

        // Restart the auto-update timer when the server comes back up (e.g. after crash-restart)
        // StartAsync relay command is NOT called on crash-restarts — ServerManagerService handles that.
        if (status == ServerStatus.Running)
        {
            WpfApplication.Current?.Dispatcher?.Invoke(() =>
            {
                StartPerfMonitoring();
                StartPlayerRefresh();
                if (_updateTimer == null) StartUpdateTimer();
            });
        }
        else if (status == ServerStatus.Stopped || status == ServerStatus.Error)
        {
            WpfApplication.Current?.Dispatcher?.Invoke(() =>
            {
                StopPerfMonitoring();
                _perfHistory.Clear(Server.Id);
                StopPlayerRefresh();
                OnlinePlayers = [];
                Server.CurrentPlayers = 0;
                if (!Server.AutoRestart)
                    StopUpdateTimer();
            });
        }
    }

    private void OnPortsReassigned(Models.GameServer srv)
    {
        if (srv.Id != Server.Id) return;
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            OnPropertyChanged(nameof(Server));
        });
    }

    private void OnCrashLimitReached(string serverId)
    {
        if (serverId != Server.Id) return;
        WpfApplication.Current?.Dispatcher?.Invoke(StopPerfMonitoring);
        StopUpdateTimer();
        _ = _notifications.NotifyAsync(
            $"⛔ {Server.DisplayName} — Auto-restart disabled",
            $"Server crashed too many times. Manual restart required.",
            "#F85149");
    }

    private void OnSteamOutput(string serverId, string line)
    {
        if (serverId.Length > 0 && serverId != Server.Id) return;
        WpfApplication.Current?.Dispatcher?.Invoke(() => AppendLog(line, ConsoleMessageType.System));
    }

    private void OnSteamProgress(string serverId, int p)
    {
        if (serverId.Length > 0 && serverId != Server.Id) return;
        WpfApplication.Current?.Dispatcher?.Invoke(() => InstallProgress = p);
    }

    private void AppendLog(string text, ConsoleMessageType type = ConsoleMessageType.Info)
        => WpfApplication.Current?.Dispatcher?.Invoke(() => Log.Add(new ConsoleMessage { Text = text, Type = type }));

    public void AppendConsoleWarning(string text)
        => AppendLog(text, ConsoleMessageType.Warning);

    private void AddActionLog(string action)
    {
        var entry = $"[{DateTime.Now:dd.MM HH:mm:ss}] {action}";
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            ActionLog.Insert(0, entry);
            if (ActionLog.Count > 100) ActionLog.RemoveAt(ActionLog.Count - 1);
        });
    }

    private void RefreshStatus()
    {
        StatusText = Server.Status switch
        {
            ServerStatus.Running      => Loc.StatusRunning,
            ServerStatus.Starting     => Loc.StatusStarting,
            ServerStatus.Stopping     => Loc.StatusStopping,
            ServerStatus.Installing   => Loc.StatusInstalling,
            ServerStatus.Updating     => Loc.StatusUpdating,
            ServerStatus.Error        => Loc.StatusError,
            ServerStatus.NotInstalled => Loc.StatusNotInstalled,
            _                         => Loc.StatusStopped,
        };
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(UptimeText));
    }

    public void Dispose()
    {
        _manager.LogReceived       -= OnLogReceived;
        _manager.StatusChanged     -= OnStatusChanged;
        _manager.CrashLimitReached -= OnCrashLimitReached;
        _manager.PortsReassigned   -= OnPortsReassigned;
        _steamCmd.OutputReceived   -= OnSteamOutput;
        _steamCmd.ProgressChanged  -= OnSteamProgress;
        _network.ServerStatsUpdated -= OnServerStatsUpdated; // #1: estää muistivuodon poistetuille palvelimille

        StopUpdateTimer();
        StopPerfMonitoring();

        _rconLock.Wait();
        try { _rcon?.Dispose(); _rcon = null; }
        finally { _rconLock.Release(); }

        _rconLock.Dispose();
    }

    // ── Plugin-specific settings fields ──────────────────────────────────────
    public List<PluginFieldVm> PluginFields { get; }
}

public partial class PluginFieldVm : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private readonly GameServer _server;
    public WGS.Games.ConfigField Field { get; }

    public PluginFieldVm(GameServer server, WGS.Games.ConfigField field)
    {
        _server = server;
        Field   = field;
    }

    public string Value
    {
        get => _server.GameSpecificSettings.TryGetValue(Field.Key, out var v) ? v : Field.DefaultValue;
        set
        {
            _server.GameSpecificSettings[Field.Key] = value;
            OnPropertyChanged();
        }
    }
}
