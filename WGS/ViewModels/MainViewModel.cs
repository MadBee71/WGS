using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WGS.Games;
using WGS.Models;
using WGS.Services;
using WGS.Views;

namespace WGS.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly ConfigService             _config;
    private readonly ServerManagerService      _manager;
    private readonly SteamCmdService           _steamCmd;
    private readonly BackupService             _backup;
    private readonly NotificationService       _notifications;
    private readonly PerformanceMonitorService _perfMonitor;
    private readonly TrayService               _tray;
    private readonly SystemMetricsService      _metrics;
    private readonly ModManagerService         _mods;
    private readonly DiscordBotService         _bot;
    private readonly ConfigEditorService       _configEditor;
    private readonly PlayerStatsService        _playerStats;
    private readonly PerfHistoryService        _perfHistory;
    private readonly SteamWorkshopService      _workshop;
    private readonly WorkshopDbService         _workshopDb;
    private readonly NetworkMonitorService     _network;
    private readonly TemplateService           _templates;
    private readonly UserService               _users;
    private readonly ServerGroupService        _groups;
    private readonly GroupBanListService       _groupBans;
    private readonly ServerHygieneService      _hygiene;
    private readonly WebApiService             _webApi;
    private readonly ScheduledTaskService      _scheduler;
    private readonly RemoteMachineService      _remoteMachines;
    private readonly CrashPredictionService    _crashPrediction;
    private readonly UPnPService               _upnp;
    private readonly WakeOnDemandService       _wakeOnDemand;
    private readonly System.Timers.Timer       _autoSaveTimer;

    public SettingsViewModel Settings { get; }
    public DashboardViewModel Dashboard { get; }
    public ObservableCollection<ServerViewModel>       Servers       { get; } = [];
    public ObservableCollection<MachineViewModel>      RemoteMachines { get; } = [];
    public ObservableCollection<RemoteServerViewModel> RemoteServers  { get; } = [];

    [ObservableProperty] private ServerViewModel?       _selectedServer;
    [ObservableProperty] private RemoteServerViewModel? _selectedRemoteServer;
    [ObservableProperty] private bool _showAddDialog;
    [ObservableProperty] private string _newServerName    = string.Empty;
    [ObservableProperty] private string _newServerInstall = string.Empty;
    [ObservableProperty] private IGamePlugin? _newServerGame;
    [ObservableProperty] private int  _newServerPort      = 7777;
    [ObservableProperty] private int  _newServerQueryPort = 27015;
    [ObservableProperty] private int  _newServerSteamPort = 0;
    [ObservableProperty] private bool _newServerHasSteamPort = false;
    [ObservableProperty] private bool _showSettingsPage;
    [ObservableProperty] private bool _showDashboard;
    [ObservableProperty] private bool _showSupport;
    [ObservableProperty] private bool _showMachines;

    // Laskettu: näytä normaali palvelinruudukko vain kun mikään sivu ei ole auki
    public bool ShowServerGrid =>
        !ShowDashboard && !ShowSupport && !ShowMachines && SelectedRemoteServer == null;

    // Laskettu: näytä etäpalvelimen hallintanäkymä
    public bool ShowRemoteDetail =>
        SelectedRemoteServer != null && !ShowDashboard && !ShowSupport && !ShowMachines;

    partial void OnShowDashboardChanged(bool _)
    {
        OnPropertyChanged(nameof(ShowServerGrid));
        OnPropertyChanged(nameof(ShowRemoteDetail));
    }
    partial void OnShowSupportChanged(bool _)
    {
        OnPropertyChanged(nameof(ShowServerGrid));
        OnPropertyChanged(nameof(ShowRemoteDetail));
    }
    partial void OnShowMachinesChanged(bool _)
    {
        OnPropertyChanged(nameof(ShowServerGrid));
        OnPropertyChanged(nameof(ShowRemoteDetail));
    }
    partial void OnSelectedRemoteServerChanged(RemoteServerViewModel? _)
    {
        OnPropertyChanged(nameof(ShowServerGrid));
        OnPropertyChanged(nameof(ShowRemoteDetail));
    }
    [ObservableProperty] private string _sortMode = "name-asc";

    // ── Update checker ────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _updateAvailable;
    [ObservableProperty] private string _latestVersion  = string.Empty;
    [ObservableProperty] private string _updateDownloadUrl = string.Empty;
    [ObservableProperty] private bool   _updateDownloading;
    [ObservableProperty] private string _updateStatusText = string.Empty;

    // ── Batch operations ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _batchMode;
    public int BatchSelectedCount => Servers.Count(s => s.IsBatchSelected);

    partial void OnBatchModeChanged(bool value)
    {
        if (!value)
            foreach (var s in Servers) s.IsBatchSelected = false;
        OnPropertyChanged(nameof(BatchSelectedCount));
    }

    // ── Add Machine dialog fields ─────────────────────────────────────────────
    [ObservableProperty] private string _newMachineName  = string.Empty;
    [ObservableProperty] private string _newMachineUrl   = string.Empty;
    [ObservableProperty] private string _newMachineToken = string.Empty;

    // ── User Management ───────────────────────────────────────────────────────
    [ObservableProperty] private string _newUserName = string.Empty;
    [ObservableProperty] private string _newUserRole = "Viewer";
    public List<Services.WgsUser> Users => _users.GetAll();

    public string[] SortModes { get; } = ["name-asc", "name-desc", "game-asc", "status"];

    public IEnumerable<ServerViewModel> SortedServers => SortMode switch
    {
        "name-desc"  => Servers.OrderByDescending(s => s.Server.DisplayName),
        "game-asc"   => Servers.OrderBy(s => s.Server.GameId),
        "status"     => Servers.OrderByDescending(s => (int)s.Server.Status),
        _            => Servers.OrderBy(s => s.Server.DisplayName),
    };

    public IEnumerable<RemoteServerViewModel> SortedRemoteServers => SortMode switch
    {
        "name-desc"  => RemoteServers.OrderByDescending(s => s.DisplayName),
        "game-asc"   => RemoteServers.OrderBy(s => s.GameId),
        "status"     => RemoteServers.OrderByDescending(s => RemoteStatusRank(s.Status)),
        _            => RemoteServers.OrderBy(s => s.DisplayName),
    };

    private static int RemoteStatusRank(string status)
        => Enum.TryParse<ServerStatus>(status, out var s) ? (int)s : -1;

    public IEnumerable<IGamePlugin> AvailableGames => GameRegistry.All.OrderBy(g => g.GameName);

    public int TotalServers  => Servers.Count;
    public int RunningCount  => Servers.Count(s => s.Server.Status == ServerStatus.Running);
    public int StoppedCount  => Servers.Count(s => s.Server.Status == ServerStatus.Stopped);

    public MainViewModel(ConfigService config, ServerManagerService manager, SteamCmdService steamCmd,
        BackupService backup, NotificationService notifications, PerformanceMonitorService perfMonitor,
        TrayService tray, SettingsViewModel settings, SystemMetricsService metrics,
        ModManagerService mods, DiscordBotService bot,
        ConfigEditorService configEditor, PlayerStatsService playerStats, PerfHistoryService perfHistory,
        SteamWorkshopService workshop, WorkshopDbService workshopDb,
        NetworkMonitorService network, TemplateService templates, UserService users,
        ServerGroupService groups, WebApiService webApi, ScheduledTaskService scheduler,
        RemoteMachineService remoteMachines, CrashPredictionService crashPrediction,
        UPnPService upnp, WakeOnDemandService wakeOnDemand, GroupBanListService groupBans,
        ServerHygieneService hygiene)
    {
        _groupBans = groupBans;
        _hygiene   = hygiene;
        _config          = config;
        _sortMode        = config.SortMode; // restore last-used sort order without triggering a save
        _manager         = manager;
        _steamCmd        = steamCmd;
        _backup          = backup;
        _notifications   = notifications;
        _perfMonitor     = perfMonitor;
        _tray            = tray;
        _metrics         = metrics;
        _mods            = mods;
        _bot             = bot;
        _configEditor    = configEditor;
        _playerStats     = playerStats;
        _perfHistory     = perfHistory;
        _workshop        = workshop;
        _network         = network;
        _templates       = templates;
        _users           = users;
        _workshopDb      = workshopDb;
        _groups          = groups;
        _webApi          = webApi;
        _scheduler       = scheduler;
        _remoteMachines  = remoteMachines;
        _crashPrediction = crashPrediction;
        _upnp            = upnp;
        _wakeOnDemand    = wakeOnDemand;
        Settings         = settings;
        Dashboard        = new DashboardViewModel(metrics, network, Servers);

        manager.PortsReassigned += srv => Save();

        manager.StatusChanged += (id, status) =>
        {
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(RunningCount));
                OnPropertyChanged(nameof(StoppedCount));
                _tray.SetStatus(RunningCount, TotalServers);
            });

            var server = Servers.FirstOrDefault(v => v.Server.Id == id)?.Server;
            if (server != null)
            {
                if (status == ServerStatus.Running)
                {
                    if (_config.EnableUPnP) _ = _upnp.AddPortsForServerAsync(server);
                    _crashPrediction.RegisterServerStart(id);
                    _wakeOnDemand.Disarm(id);
                    _wakeOnDemand.ArmIdleShutdown(server);
                }
                else if (status is ServerStatus.Stopped or ServerStatus.Error)
                {
                    if (_config.EnableUPnP) _ = _upnp.RemovePortsForServerAsync(server);
                    _wakeOnDemand.DisarmIdleShutdown(id);
                    _wakeOnDemand.Arm(server);
                }
            }
        };

        Servers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SortedServers));
        RemoteServers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SortedRemoteServers));
        LoadServers();
        _ = CheckForUpdateAsync();

        // Auto-save server settings periodically so changes survive app restart
        // without requiring the user to press the Save button.
        _autoSaveTimer = new System.Timers.Timer(5000) { AutoReset = true };
        _autoSaveTimer.Elapsed += (_, _) => Save();
        _autoSaveTimer.Start();

        // Wire up Discord bot callbacks — same Dispatcher fix as WebAPI
        _bot.GetServers    = () => Servers.Select(v => v.Server);
        _bot.StartServer   = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.StartCommand.ExecuteAsync(null)        : Task.CompletedTask; });
        _bot.StopServer    = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.StopCommand.ExecuteAsync(null)         : Task.CompletedTask; });
        _bot.RestartServer = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.RestartCommand.ExecuteAsync(null)      : Task.CompletedTask; });
        _bot.UpdateServer  = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.UpdateCommand.ExecuteAsync(null)       : Task.CompletedTask; });
        _bot.BackupServer  = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.CreateBackupCommand.ExecuteAsync(null) : Task.CompletedTask; });
        _bot.SendCmd       = async (id, cmd) => await manager.SendCommandAsync(id, cmd);

        // Start bot if already configured
        _bot.ApplySettings(notifications.Settings);

        // Wire Scheduled Task callbacks
        _scheduler.GetServers   = () => Servers.Select(v => v.Server);
        _scheduler.UpdateServer = async id => { var vm = FindServer(id); if (vm != null) await vm.UpdateCommand.ExecuteAsync(null); };

        // Wire Web API callbacks
        _webApi.GetServers    = () => Servers.Select(v => v.Server);
        // AsyncRelayCommand.ExecuteAsync monitors task completion and calls
        // ButtonBase.UpdateCanExecute() — a DependencyProperty access that requires the
        // UI thread. Always dispatch through the Dispatcher so WPF never sees a
        // cross-thread call, regardless of which thread the HTTP handler runs on.
        // Fire-and-forget (return Task.CompletedTask immediately) so the HTTP response
        // is not held open for the full duration of long operations like restart.
        static Task DispatchCommand(Func<Task> action)
        {
            WpfApplication.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                try { await action(); } catch { }
            });
            return Task.CompletedTask;
        }
        _webApi.StartServer   = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.StartCommand.ExecuteAsync(null)         : Task.CompletedTask; });
        _webApi.StopServer    = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.StopCommand.ExecuteAsync(null)          : Task.CompletedTask; });
        _webApi.RestartServer = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.RestartCommand.ExecuteAsync(null)       : Task.CompletedTask; });
        _webApi.UpdateServer  = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.UpdateCommand.ExecuteAsync(null)        : Task.CompletedTask; });
        _webApi.BackupServer  = id => DispatchCommand(() => { var vm = FindServer(id); return vm != null ? vm.CreateBackupCommand.ExecuteAsync(null)  : Task.CompletedTask; });
        _webApi.SendCmd       = async (id, cmd) => await manager.SendCommandAsync(id, cmd);
        _webApi.GetMetrics    = () => metrics.Current;
        _webApi.GetNetwork    = () => (network.CurrentBytesInPerSec, network.CurrentBytesOutPerSec);
        _webApi.GetLog = (id, offset) =>
        {
            var inst = manager.GetInstance(id);
            if (inst == null) return ([], [], offset);
            var all   = inst.GetLogSnapshot();
            var slice = all.Skip(offset).ToList();
            return (
                slice.Select(m => m.Text).ToList(),
                slice.Select(m => m.Type.ToString()).ToList(),
                offset + slice.Count
            );
        };
        _webApi.Users     = users;
        _webApi.GetUptime = id => manager.GetInstance(id)?.Uptime is TimeSpan t && t > TimeSpan.Zero
            ? $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
            : null;
        _webApi.GetOnlinePlayers = id =>
            FindServer(id)?.OnlinePlayers ?? [];
        _webApi.GetPerfSamples = (id, minutes) =>
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            return _perfHistory.Get(id).Where(s => s.Time >= cutoff)
                .Select(s => (s.Time, s.Cpu, s.MemMb));
        };
        _webApi.GetBackups = id =>
        {
            GameServer? srv = null;
            WpfApplication.Current?.Dispatcher?.Invoke(() => { srv = FindServer(id)?.Server; });
            if (srv == null) return [];
            return _backup.GetBackupsForServer(srv)
                .Select(b => (
                    fileName:  System.IO.Path.GetFileName(b.FilePath),
                    sizeText:  b.SizeText,
                    createdAt: b.CreatedAt));
        };
        _webApi.RestoreBackup = async (id, fileName) =>
        {
            GameServer? srv = null;
            WpfApplication.Current?.Dispatcher?.Invoke(() => { srv = FindServer(id)?.Server; });
            if (srv == null) return "Server not found";
            var dir      = System.IO.Path.GetFullPath(System.IO.Path.Combine(_backup.BackupRoot, id));
            var zipPath  = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, fileName));
            // Ensure the resolved path is strictly within the server backup directory
            if (!zipPath.StartsWith(dir + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return "Invalid backup path";
            if (!System.IO.File.Exists(zipPath)) return "Backup file not found";
            try { await _backup.RestoreBackupAsync(srv, zipPath); return null; }
            catch (Exception ex) { return ex.Message; }
        };

        // Wire RemoteMachineService
        foreach (var machine in _remoteMachines.Machines)
            RemoteMachines.Add(new MachineViewModel(machine, _remoteMachines));

        _remoteMachines.MachineUpdated += OnMachineUpdated;
        _remoteMachines.MachinesChanged += OnMachinesChanged;

        // Wire CrashPredictionService — snapshot on UI thread so background thread gets a safe copy
        _crashPrediction.GetRunningServers = () =>
            WpfApplication.Current?.Dispatcher?.Invoke(() =>
                Servers.Where(v => v.Server.Status == ServerStatus.Running)
                       .Select(v => (v.Server.Id, v.Server.DisplayName, v.Server.CurrentPlayers))
                       .ToList()
                       .AsEnumerable()
            ) ?? [];
        _crashPrediction.PredictionRaised += OnCrashPrediction;
        manager.LogReceived += (id, msg) =>
        {
            var name = WpfApplication.Current?.Dispatcher?.Invoke(() =>
                Servers.FirstOrDefault(v => v.Server.Id == id)?.Server.DisplayName);
            if (name != null) _crashPrediction.CheckLogLine(id, name, msg.Text);
        };
    }

    private void OnMachineUpdated(string machineId, List<Services.RemoteServerInfo> servers)
    {
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            var machineVm = RemoteMachines.FirstOrDefault(m => m.Definition.Id == machineId);
            if (machineVm != null)
            {
                machineVm.IsOnline = servers.Count > 0 || _remoteMachines.IsOnline(machineId);
                machineVm.UpdateServers(servers);
            }

            // Sync RemoteServers sidebar list
            var machineDef = _remoteMachines.Machines.FirstOrDefault(m => m.Id == machineId);
            if (machineDef == null) return;

            foreach (var info in servers)
            {
                var existing = RemoteServers.FirstOrDefault(r => r.ServerId == info.Id && r.MachineName == machineDef.Name);
                if (existing != null)
                    existing.UpdateInfo(info);
                else
                    RemoteServers.Add(new RemoteServerViewModel(machineId, machineDef.Name, info, _remoteMachines));
            }
            // Remove servers that disappeared
            var ids = servers.Select(s => s.Id).ToHashSet();
            foreach (var gone in RemoteServers.Where(r => r.MachineName == machineDef.Name && !ids.Contains(r.ServerId)).ToList())
            {
                if (SelectedRemoteServer == gone) { gone.StopPolling(); SelectedRemoteServer = null; }
                gone.Dispose();
                RemoteServers.Remove(gone);
            }
        });
    }

    private void OnMachinesChanged()
    {
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            RemoteMachines.Clear();
            foreach (var machine in _remoteMachines.Machines)
                RemoteMachines.Add(new MachineViewModel(machine, _remoteMachines));

            // Clear remote servers that belong to removed machines
            var machineNames = _remoteMachines.Machines.Select(m => m.Name).ToHashSet();
            foreach (var gone in RemoteServers.Where(r => !machineNames.Contains(r.MachineName)).ToList())
            {
                if (SelectedRemoteServer == gone) { gone.StopPolling(); SelectedRemoteServer = null; }
                gone.Dispose();
                RemoteServers.Remove(gone);
            }
        });
    }

    private void OnCrashPrediction(string serverId, Services.CrashPrediction pred)
    {
        // FindServer accesses Servers collection — must run on UI thread
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            FindServer(serverId)?.AppendConsoleWarning(
                $"[WGS PREDICTION] {pred.Reason} (Severity {pred.Severity})");
        });

        // NotifyAsync is fire-and-forget, safe to call from any thread
        if (Settings.CrashPredictionDiscord)
        {
            var color = pred.Severity >= 2 ? "#F85149" : "#D29922";
            _ = _notifications.NotifyAsync(
                $"⚠ Crash predicted: {pred.ServerName}",
                $"{pred.Reason}\nSeverity: {(pred.Severity >= 2 ? "Critical" : "Warning")}",
                color);
        }
    }

    private void LoadServers()
    {
        int num = 1;
        foreach (var srv in _config.LoadServers())
        {
            srv.Status = ServerStatus.Stopped;
            bool reattached = _manager.TryReattach(srv);
            var vm = MakeVm(srv);
            vm.ServerNumber = num++;
            Servers.Add(vm);
            if (srv.AutoStart && !reattached)
                _ = WpfApplication.Current?.Dispatcher?.InvokeAsync(() => vm.StartCommand.ExecuteAsync(null))
                        .Task.ContinueWith(t => Console.WriteLine($"[WGS] AutoStart failed for {srv.DisplayName}: {t.Exception?.InnerException?.Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
        }
        SelectedServer = Servers.FirstOrDefault();
        RefreshCounts();
        _tray.SetStatus(RunningCount, TotalServers);
    }

    private ServerViewModel? FindServer(string id) => Servers.FirstOrDefault(v => v.Server.Id == id);

    [RelayCommand]
    private void ToggleBatchMode() => BatchMode = !BatchMode;

    [RelayCommand]
    private void BatchSelectAll()
    {
        foreach (var s in Servers) s.IsBatchSelected = true;
        OnPropertyChanged(nameof(BatchSelectedCount));
    }

    [RelayCommand]
    private void BatchClearSelection()
    {
        foreach (var s in Servers) s.IsBatchSelected = false;
        OnPropertyChanged(nameof(BatchSelectedCount));
    }

    [RelayCommand]
    private async Task BatchStartAsync()
    {
        var targets = Servers.Where(s => s.IsBatchSelected && s.CanStart).ToList();
        foreach (var vm in targets)
            await vm.StartCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task BatchStopAsync()
    {
        var targets = Servers.Where(s => s.IsBatchSelected && s.CanStop).ToList();
        foreach (var vm in targets)
            await vm.StopCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task BatchRestartAsync()
    {
        var targets = Servers.Where(s => s.IsBatchSelected && s.IsRunning).ToList();
        foreach (var vm in targets)
            await vm.RestartCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task BatchBackupAsync()
    {
        var targets = Servers.Where(s => s.IsBatchSelected).ToList();
        foreach (var vm in targets)
            await vm.CreateBackupCommand.ExecuteAsync(null);
    }

    private async Task CheckForUpdateAsync()
    {
        var (hasUpdate, latest, url) = await Services.UpdateCheckerService.CheckAsync();
        if (hasUpdate)
        {
            UpdateAvailable      = true;
            LatestVersion        = latest;
            UpdateDownloadUrl    = url;
        }
    }

    [RelayCommand]
    private async Task PerformUpdateAsync()
    {
        // Count running servers
        var running = Servers.Where(s => s.IsRunning).ToList();

        // Build confirmation message
        string msg = running.Count > 0
            ? $"WGS will update to {LatestVersion}.\n\n" +
              $"{running.Count} server{(running.Count == 1 ? "" : "s")} {(running.Count == 1 ? "is" : "are")} currently running and will be stopped before the update.\n\n" +
              "WGS will restart automatically after the update."
            : $"WGS will update to {LatestVersion} and restart automatically.";

        var result = System.Windows.MessageBox.Show(
            msg,
            "Update WGS",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information);

        if (result != System.Windows.MessageBoxResult.OK) return;

        UpdateDownloading = true;
        UpdateStatusText  = "Stopping servers...";

        // Stop all running servers gracefully
        foreach (var vm in running)
        {
            try { await vm.StopCommand.ExecuteAsync(null); }
            catch { }
        }

        // Download and prepare update
        var progress = new Progress<(int pct, string msg)>(x =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(
                () => UpdateStatusText = $"[{x.pct}%] {x.msg}");
        });

        var ok = await Services.SelfUpdateService.DownloadAndPrepareAsync(
            UpdateDownloadUrl, progress);

        if (!ok)
        {
            UpdateDownloading = false;
            UpdateStatusText  = string.Empty;
            System.Windows.MessageBox.Show(
                "Download failed. Opening GitHub releases page instead.",
                "Update failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                Services.UpdateCheckerService.ReleasesUrl) { UseShellExecute = true }); }
            catch { }
            return;
        }

        Services.SelfUpdateService.ApplyAndRestart();
    }

    private ServerViewModel MakeVm(GameServer srv)
    {
        var vm = new ServerViewModel(srv, _manager, _steamCmd, _backup, _notifications, _perfMonitor, _config, _mods,
               _configEditor, _playerStats, _perfHistory, _workshop, _workshopDb, _templates, _scheduler, _network,
               _groupBans, _hygiene);
        vm.BatchSelectionChanged = () => OnPropertyChanged(nameof(BatchSelectedCount));
        return vm;
    }

    [RelayCommand]
    private void OpenAddDialog()
    {
        NewServerName    = string.Empty;
        NewServerGame    = AvailableGames.FirstOrDefault();
        // ports are set by OnNewServerGameChanged above
        ShowAddDialog    = true;
    }

    partial void OnSelectedServerChanged(ServerViewModel? value)
    {
        if (value != null)
        {
            ShowDashboard      = false;
            ShowSupport        = false;
            ShowMachines       = false;
            ShowSettingsPage   = false;
            SelectedRemoteServer?.StopPolling();
            SelectedRemoteServer = null;
        }
    }

    partial void OnSelectedRemoteServerChanged(RemoteServerViewModel? oldValue, RemoteServerViewModel? newValue)
    {
        oldValue?.StopPolling();
        if (newValue != null)
        {
            SelectedServer   = null;
            ShowDashboard    = false;
            ShowSupport      = false;
            ShowMachines     = false;
            ShowSettingsPage = false;
            newValue.StartPolling();
        }
    }
    partial void OnSortModeChanged(string value)
    {
        OnPropertyChanged(nameof(SortedServers));
        OnPropertyChanged(nameof(SortedRemoteServers));
        _config.SortMode = value;
        _config.Save();
    }
    partial void OnNewServerGameChanged(IGamePlugin? value)
    {
        UpdateInstallPath();
        if (value == null) return;
        // Auto-assign free ports, incrementing by 1 until no conflict
        var (gp, qp, sp) = AllocatePorts(value.DefaultPort, value.DefaultQueryPort, value.DefaultSteamPort);
        NewServerPort         = gp;
        NewServerQueryPort    = qp;
        NewServerSteamPort    = sp;
        NewServerHasSteamPort = value.DefaultSteamPort > 0;
    }
    partial void OnNewServerNameChanged(string value)      => UpdateInstallPath();

    private void UpdateInstallPath()
    {
        if (NewServerGame == null) return;
        var baseName = string.IsNullOrWhiteSpace(NewServerName)
            ? NewServerGame.GameId
            : string.Join("_", NewServerName.Split(System.IO.Path.GetInvalidFileNameChars()));
        // Install directly next to where the .exe will live — one folder per server
        NewServerInstall = System.IO.Path.Combine(
            _config.DefaultInstallRoot,
            NewServerGame.GameId,
            baseName);
    }

    [RelayCommand]
    private void CloseAddDialog() => ShowAddDialog = false;

    [RelayCommand]
    private void ConfirmAddServer()
    {
        if (NewServerGame == null || string.IsNullOrWhiteSpace(NewServerName)) return;

        var srv = new GameServer
        {
            GameId        = NewServerGame.GameId,
            DisplayName   = NewServerName,
            ServerName    = NewServerName,
            InstallPath   = NewServerInstall,
            ServerPort    = NewServerPort,
            QueryPort     = NewServerQueryPort,
            SteamPort     = NewServerSteamPort,
            MaxPlayers    = NewServerGame.DefaultMaxPlayers,
            Status        = ServerStatus.NotInstalled,
            GameSpecificSettings = NewServerGame.GetDefaultSettings(),
        };

        var vm = MakeVm(srv);
        vm.ServerNumber = Servers.Count + 1;
        Servers.Add(vm);
        SelectedServer = vm;
        ShowAddDialog  = false;
        Save();
        RefreshCounts();
    }

    [RelayCommand]
    private void CloneServer(ServerViewModel? source)
    {
        if (source == null) return;

        var src = source.Server;
        var plugin = GameRegistry.Get(src.GameId);
        if (plugin == null) return;

        // Allocate fresh ports so clone doesn't conflict
        var (gp, qp, sp) = AllocatePorts(src.ServerPort, src.QueryPort, src.SteamPort);

        // Build a unique name and install path
        var cloneName = $"{src.DisplayName} (Copy)";
        var safeName  = string.Join("_", cloneName.Split(System.IO.Path.GetInvalidFileNameChars()));
        var clonePath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(src.InstallPath) ?? _config.DefaultInstallRoot,
            safeName);

        var clone = new GameServer
        {
            GameId               = src.GameId,
            DisplayName          = cloneName,
            ServerName           = cloneName,
            InstallPath          = clonePath,
            ServerPort           = gp,
            QueryPort            = qp,
            SteamPort            = sp,
            RconPort             = src.RconPort > 0 ? gp + 10 : 0,
            MaxPlayers           = src.MaxPlayers,
            AutoRestart          = src.AutoRestart,
            AutoUpdate           = src.AutoUpdate,
            DailyRestartEnabled  = src.DailyRestartEnabled,
            DailyRestartTime     = src.DailyRestartTime,
            DiscordAlertsEnabled = src.DiscordAlertsEnabled,
            BackupEnabled        = src.BackupEnabled,
            BackupRetention      = src.BackupRetention,
            Gslt                 = src.Gslt,
            CpuAffinityMask      = src.CpuAffinityMask,
            ProcessPriority      = src.ProcessPriority,
            Status               = ServerStatus.NotInstalled,
            GameSpecificSettings = new Dictionary<string, string>(src.GameSpecificSettings),
        };

        var vm = MakeVm(clone);
        vm.ServerNumber = Servers.Count + 1;
        Servers.Add(vm);
        SelectedServer = vm;
        Save();
        RefreshCounts();
    }

    [RelayCommand]
    private async Task RemoveServerAsync(ServerViewModel? vm)
    {
        if (vm == null) return;

        var dlg = new WGS.Views.RemoveServerDialog(vm.Server.DisplayName)
        {
            Owner = WpfApplication.Current.MainWindow,
        };
        dlg.ShowDialog();

        if (dlg.Result == WGS.Views.RemoveServerResult.Cancel) return;

        if (vm.IsRunning)
        {
            await vm.StopCommand.ExecuteAsync(null);
            // Wait for the process to fully release file handles before deleting
            var inst = _manager.GetInstance(vm.Server.Id);
            if (inst?.Process != null)
            {
                try { await inst.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch { /* timeout or already exited */ }
            }
            await Task.Delay(500); // extra buffer for OS handle release
        }

        if (dlg.Result == WGS.Views.RemoveServerResult.RemoveWithFiles
            && System.IO.Directory.Exists(vm.Server.InstallPath))
        {
            try { System.IO.Directory.Delete(vm.Server.InstallPath, recursive: true); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"File deletion failed:\n{ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        vm.Dispose();
        Servers.Remove(vm);
        if (SelectedServer == vm) SelectedServer = Servers.FirstOrDefault();
        Save();
        RefreshCounts();
    }

    [RelayCommand]
    private async Task BackupAllAsync()
    {
        foreach (var vm in Servers)
            await vm.CreateBackupCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void OpenDashboard()
    {
        SelectedServer   = null;
        ShowDashboard    = true;
        ShowSettingsPage = false;
        ShowSupport      = false;
        ShowMachines     = false;
        DebugLog("OpenDashboard");
    }

    [RelayCommand]
    private void OpenSupport()
    {
        SelectedServer   = null;
        ShowSupport      = true;
        ShowDashboard    = false;
        ShowSettingsPage = false;
        ShowMachines     = false;
        DebugLog("OpenSupport");
    }

    [RelayCommand]
    private void OpenMachines()
    {
        SelectedServer   = null;
        ShowMachines     = true;
        ShowDashboard    = false;
        ShowSettingsPage = false;
        ShowSupport      = false;
        DebugLog("OpenMachines");
    }

    // ── User Management ───────────────────────────────────────────────────────

    [ObservableProperty] private string _userChangePassword = string.Empty;
    public List<Services.AuditEntry> AuditLog => _users.GetAuditLog(50);

    [RelayCommand]
    private void AddUser(System.Windows.Controls.PasswordBox? pwBox)
    {
        if (string.IsNullOrWhiteSpace(NewUserName) || pwBox == null) return;
        var role = Enum.TryParse<Services.UserRole>(NewUserRole, out var r) ? r : Services.UserRole.Viewer;
        _users.CreateUser(NewUserName, pwBox.Password, role);
        _users.WriteAudit("admin", "create_user", $"user={NewUserName} role={role}");
        NewUserName = string.Empty;
        pwBox.Clear();
        RefreshUsers();
    }

    [RelayCommand]
    private void RegenerateToken(Services.WgsUser? user)
    {
        if (user == null) return;
        _users.RegenerateToken(user.Id, "admin");
        RefreshUsers();
    }

    [RelayCommand]
    private void ToggleEnabled(Services.WgsUser? user)
    {
        if (user == null) return;
        _users.SetEnabled(user.Id, !user.IsEnabled, "admin");
        RefreshUsers();
    }

    [RelayCommand]
    private void ChangeUserRole(Services.WgsUser? user)
    {
        if (user == null) return;
        var newRole = user.Role == Services.UserRole.Admin
            ? Services.UserRole.Viewer
            : Services.UserRole.Admin;
        _users.ChangeRole(user.Id, newRole, "admin");
        RefreshUsers();
    }

    [RelayCommand]
    private void ChangeUserPassword(Services.WgsUser? user)
    {
        if (user == null || string.IsNullOrWhiteSpace(UserChangePassword)) return;
        _users.ChangePassword(user.Id, UserChangePassword, "admin");
        UserChangePassword = string.Empty;
        RefreshUsers();
    }

    [RelayCommand]
    private void DeleteUser(Services.WgsUser? user)
    {
        if (user == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Delete user \"{user.Username}\"?", "Confirm",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        _users.DeleteUser(user.Id, "admin");
        RefreshUsers();
    }

    [RelayCommand]
    private void RefreshAuditLog() => OnPropertyChanged(nameof(AuditLog));

    private void RefreshUsers()
    {
        OnPropertyChanged(nameof(Users));
        OnPropertyChanged(nameof(AuditLog));
    }

    // ── Remote Machines (Master side) ────────────────────────────────────────

    [RelayCommand]
    private void AddMachine()
    {
        if (string.IsNullOrWhiteSpace(NewMachineUrl)) return;
        var def = new Models.MachineDefinition
        {
            Name    = string.IsNullOrWhiteSpace(NewMachineName) ? NewMachineUrl : NewMachineName,
            Url     = NewMachineUrl.TrimEnd('/'),
            Token   = NewMachineToken,
            Enabled = true,
        };
        _remoteMachines.AddMachine(def);
        NewMachineName  = string.Empty;
        NewMachineUrl   = string.Empty;
        NewMachineToken = string.Empty;
    }

    [RelayCommand]
    private void RemoveMachine(MachineViewModel? vm)
    {
        if (vm == null) return;
        _remoteMachines.RemoveMachine(vm.Definition.Id);
    }

    [RelayCommand]
    private void OpenPluginCreator()
    {
        var vm  = new PluginCreatorViewModel();
        var win = new PluginCreatorView(vm);
        win.Owner = WpfApplication.Current.MainWindow;
        bool created = false;
        vm.PluginCreated += () => created = true;
        win.ShowDialog();
        if (created)
        {
            OnPropertyChanged(nameof(AvailableGames));
            System.Windows.MessageBox.Show(
                $"✅ Game module '{vm.GameName}' created! You can now add a server using it.",
                "Module created",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void ImportCsPlugin()
    {
        using var dlg = new System.Windows.Forms.OpenFileDialog
        {
            Title  = "Import plugin (.cs)",
            Filter = "C# source files (*.cs)|*.cs",
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var (plugin, error) = WGS.Services.PluginCompilerService.CompileAndLoad(dlg.FileName);
        if (plugin == null)
        {
            System.Windows.MessageBox.Show(error, "Import failed",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        if (GameRegistry.All.Any(p => p.GameId == plugin.GameId))
        {
            System.Windows.MessageBox.Show(
                $"Plugin '{plugin.GameId}' is already registered.",
                "Import", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        GameRegistry.Register(plugin);
        OnPropertyChanged(nameof(AvailableGames));
        System.Windows.MessageBox.Show(
            $"✅ Plugin '{plugin.GameName}' imported successfully!",
            "Import", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ExportCsPlugin()
    {
        // Pick which plugin to export
        var all = AvailableGames.ToList();
        if (all.Count == 0) return;

        var win = new WGS.Views.ExportPluginDialog(all);
        win.Owner = WpfApplication.Current.MainWindow;
        if (win.ShowDialog() != true || win.SelectedPlugin == null) return;

        var plugin = win.SelectedPlugin;
        using var save = new System.Windows.Forms.SaveFileDialog
        {
            Title            = "Export plugin as .cs",
            Filter           = "C# source files (*.cs)|*.cs",
            FileName         = $"{plugin.GameId}_plugin.cs",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (save.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        try
        {
            WGS.Services.PluginExporterService.ExportToFile(plugin, save.FileName);
            System.Windows.MessageBox.Show(
                $"✅ Plugin '{plugin.GameName}' exported to:\n{save.FileName}",
                "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Export failed: {ex.Message}", "Export",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var win = new WGS.Views.SettingsWindow(Settings, this);
        win.Owner = WpfApplication.Current.MainWindow;
        win.ShowDialog();
    }

    [RelayCommand]
    public void Save() => _config.SaveServers(Servers.Select(v => v.Server));

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(TotalServers));
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(StoppedCount));
    }

    private static void DebugLog(string msg)
    {
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WGS", "crash.log");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] NAV: {msg}\n");
        }
        catch { }
    }

    /// <summary>
    /// Returns all port numbers already assigned to any existing WGS server.
    /// </summary>
    private HashSet<int> UsedPorts()
    {
        var used = new HashSet<int>();
        foreach (var vm in Servers)
        {
            used.Add(vm.Server.ServerPort);
            if (vm.Server.QueryPort  > 0) used.Add(vm.Server.QueryPort);
            if (vm.Server.SteamPort  > 0) used.Add(vm.Server.SteamPort);
        }
        return used;
    }

    /// <summary>
    /// Given default ports from a plugin, increments each until it finds
    /// a combination where no port overlaps with existing servers.
    /// Keeps the relative offsets between game/query/steam intact.
    /// </summary>
    private (int game, int query, int steam) AllocatePorts(int defGame, int defQuery, int defSteam)
    {
        var used  = UsedPorts();
        int offset = 0;
        while (offset < 1000)
        {
            int gp = defGame  + offset;
            int qp = defQuery > 0 ? defQuery + offset : 0;
            int sp = defSteam > 0 ? defSteam + offset : 0;

            bool clash = used.Contains(gp)
                      || (qp > 0 && used.Contains(qp))
                      || (sp > 0 && used.Contains(sp));
            if (!clash)
                return (gp, qp > 0 ? qp : defQuery, sp);

            offset++;
        }
        // Fallback: return defaults unchanged
        return (defGame, defQuery, defSteam);
    }
}
