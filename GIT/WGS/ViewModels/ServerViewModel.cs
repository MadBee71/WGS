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
    private readonly ScheduledTaskService  _scheduler;
    private RconService? _rcon;
    private readonly SemaphoreSlim _rconLock  = new(1, 1);
    private readonly object        _perfLock  = new();
    private System.Timers.Timer?   _updateTimer;

    public GameServer Server { get; }
    public IGamePlugin? Plugin { get; }
    public ObservableCollection<ConsoleMessage> Log { get; } = [];
    public ObservableCollection<BackupEntry> Backups { get; } = [];

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

    // Players
    [ObservableProperty] private List<Services.PlayerSession> _playerHistory = [];

    // Performance history
    [ObservableProperty] private OxyPlot.PlotModel _perfPlot = CreateEmptyPlot();
    private OxyPlot.PlotModel?          _perfModel;
    private OxyPlot.Series.LineSeries?  _cpuSeries;
    private OxyPlot.Series.LineSeries?  _memSeries;

    // Workshop
    [ObservableProperty] private List<Services.WorkshopItem> _workshopItems = [];
    [ObservableProperty] private string _workshopItemId = string.Empty;
    [ObservableProperty] private bool   _workshopBusy;

    // Scheduled tasks
    [ObservableProperty] private List<Services.ScheduledTask> _scheduledTasks = [];

    public bool HasWorkshop => _workshop.SupportsWorkshop(Plugin);

    private System.Timers.Timer? _perfTimer;

    public bool HasModSupport       => Plugin?.SupportsOxide == true
                                    || !string.IsNullOrEmpty(Plugin?.MinecraftFlavor);
    public bool HasMinecraftSupport => !string.IsNullOrEmpty(Plugin?.MinecraftFlavor);

    public List<CpuCoreItem> CpuCores { get; }
    public string[] PriorityOptions { get; } = ["Normal", "AboveNormal", "High", "BelowNormal", "RealTime"];

    public string StatusColor => Server.Status switch
    {
        ServerStatus.Running      => "#3FB950",
        ServerStatus.Starting     => "#D29922",
        ServerStatus.Stopping     => "#D29922",
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
        PerfHistoryService perfHistory, SteamWorkshopService workshop,
        ScheduledTaskService scheduler)
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
        _scheduler     = scheduler;

        _manager.LogReceived      += OnLogReceived;
        _manager.StatusChanged    += OnStatusChanged;
        _manager.CrashLimitReached += OnCrashLimitReached;
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

        RefreshStatus();
        RefreshBackups();
    }

    // ── Start / Stop / Restart ──────────────────────────────────────────────

    [RelayCommand]
    private async Task StartAsync()
    {
        try
        {
            if (Server.AutoUpdate && Plugin?.SteamAppId > 0)
                await InstallAsync();
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
            await _steamCmd.InstallOrUpdateAsync(Plugin.SteamAppId, Server.InstallPath, login, password, Plugin.SteamBranch);
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

    [RelayCommand]
    private async Task RefreshPlayersAsync()
    {
        // Fetch live player list via RCON
        if (RconConnected && _rcon != null)
        {
            await _rconLock.WaitAsync();
            try
            {
                var resp = await _rcon.SendCommandAsync("status");
                AppendLog("[Players] " + resp, ConsoleMessageType.System);
            }
            finally { _rconLock.Release(); }
        }
        // Load history from SQLite
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
            PlayerHistory = _playerStats.GetSessions(Server.Id, 50));
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
        });
        _perfModel.Series.Add(_cpuSeries);
        _perfModel.Series.Add(_memSeries);
        WpfApplication.Current?.Dispatcher?.Invoke(() => PerfPlot = _perfModel);
    }

    private void UpdatePerfChart()
    {
        var samples = _perfHistory.Get(Server.Id);
        if (samples.Count == 0) return;
        EnsurePerfModel();
        _cpuSeries!.Points.Clear();
        _memSeries!.Points.Clear();
        foreach (var s in samples)
        {
            var x = OxyPlot.Axes.DateTimeAxis.ToDouble(s.Time);
            _cpuSeries.Points.Add(new OxyPlot.DataPoint(x, s.Cpu));
            _memSeries.Points.Add(new OxyPlot.DataPoint(x, s.MemMb));
        }
        WpfApplication.Current?.Dispatcher?.Invoke(() => _perfModel!.InvalidatePlot(false));
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
        WorkshopItems = await _workshop.GetInstalledItemsAsync(Server, Plugin);
    }

    [RelayCommand]
    private async Task InstallWorkshopItemAsync()
    {
        if (!ulong.TryParse(WorkshopItemId, out var id)) { AppendLog("[Workshop] Invalid item ID.", ConsoleMessageType.Warning); return; }
        if (Plugin == null || !HasWorkshop) return;
        WorkshopBusy = true;
        try
        {
            var progress = new Progress<(int pct, string msg)>(x =>
                WpfApplication.Current?.Dispatcher?.Invoke(() => AppendLog($"[Workshop] [{x.pct}%] {x.msg}", ConsoleMessageType.System)));
            await _workshop.InstallItemAsync(Server, Plugin, id, progress);
            AppendLog($"[Workshop] ✅ Item {id} installed.", ConsoleMessageType.System);
            WorkshopItemId = string.Empty;
            await RefreshWorkshopAsync();
        }
        catch (Exception ex) { AppendLog($"[Workshop] ❌ {ex.Message}", ConsoleMessageType.Error); }
        finally { WorkshopBusy = false; }
    }

    [RelayCommand]
    private async Task UninstallWorkshopItemAsync(WorkshopItem? item)
    {
        if (item == null || Plugin == null) return;
        _workshop.UninstallItem(Server, Plugin, item.PublishedFileId);
        AppendLog($"[Workshop] Removed item {item.PublishedFileId}.", ConsoleMessageType.System);
        await RefreshWorkshopAsync();
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

    private void StopPerfMonitoring()
    {
        lock (_perfLock)
        {
            _perfTimer?.Stop();
            _perfTimer?.Dispose();
            _perfTimer = null;
        }
        _perfMonitor.Untrack(Server.Id);
        _perfHistory.Clear(Server.Id);
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

        // Restart the auto-update timer when the server comes back up (e.g. after crash-restart)
        // StartAsync relay command is NOT called on crash-restarts — ServerManagerService handles that.
        if (status == ServerStatus.Running)
        {
            WpfApplication.Current?.Dispatcher?.Invoke(() =>
            {
                StartPerfMonitoring();
                if (_updateTimer == null) StartUpdateTimer();
            });
        }
        else if (status == ServerStatus.Stopped || status == ServerStatus.Error)
        {
            WpfApplication.Current?.Dispatcher?.Invoke(() =>
            {
                StopPerfMonitoring();
                // Only stop timer if not auto-restarting (AutoRestart handles re-start itself)
                if (!Server.AutoRestart)
                    StopUpdateTimer();
            });
        }
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

    private void OnSteamOutput(string line)
        => WpfApplication.Current?.Dispatcher?.Invoke(() => AppendLog(line, ConsoleMessageType.System));

    private void OnSteamProgress(int p)
        => WpfApplication.Current?.Dispatcher?.Invoke(() => InstallProgress = p);

    private void AppendLog(string text, ConsoleMessageType type = ConsoleMessageType.Info)
        => WpfApplication.Current?.Dispatcher?.Invoke(() => Log.Add(new ConsoleMessage { Text = text, Type = type }));

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
        _steamCmd.OutputReceived   -= OnSteamOutput;
        _steamCmd.ProgressChanged  -= OnSteamProgress;

        StopUpdateTimer();
        StopPerfMonitoring();

        _rconLock.Wait();
        try { _rcon?.Dispose(); _rcon = null; }
        finally { _rconLock.Release(); }

        _rconLock.Dispose();
    }
}
