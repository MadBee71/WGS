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
    private readonly ServerGroupService        _groups;
    private readonly WebApiService             _webApi;
    private readonly ScheduledTaskService      _scheduler;
    private readonly RemoteMachineService      _remoteMachines;
    private readonly CrashPredictionService    _crashPrediction;

    public SettingsViewModel Settings { get; }
    public DashboardViewModel Dashboard { get; }
    public ObservableCollection<ServerViewModel> Servers { get; } = [];
    public ObservableCollection<MachineViewModel> RemoteMachines { get; } = [];

    [ObservableProperty] private ServerViewModel? _selectedServer;
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
    [ObservableProperty] private string _sortMode = "name-asc";

    public string[] SortModes { get; } = ["name-asc", "name-desc", "game-asc", "status"];

    public IEnumerable<ServerViewModel> SortedServers => SortMode switch
    {
        "name-desc"  => Servers.OrderByDescending(s => s.Server.DisplayName),
        "game-asc"   => Servers.OrderBy(s => s.Server.GameId),
        "status"     => Servers.OrderByDescending(s => (int)s.Server.Status),
        _            => Servers.OrderBy(s => s.Server.DisplayName),
    };

    public IEnumerable<IGamePlugin> AvailableGames => GameRegistry.All.OrderBy(g => g.GameName);

    public int TotalServers  => Servers.Count;
    public int RunningCount  => Servers.Count(s => s.Server.Status == ServerStatus.Running);
    public int StoppedCount  => Servers.Count(s => s.Server.Status == ServerStatus.Stopped);

    public MainViewModel(ConfigService config, ServerManagerService manager, SteamCmdService steamCmd,
        BackupService backup, NotificationService notifications, PerformanceMonitorService perfMonitor,
        TrayService tray, SettingsViewModel settings, SystemMetricsService metrics,
        ModManagerService mods, DiscordBotService bot,
        ConfigEditorService configEditor, PlayerStatsService playerStats, PerfHistoryService perfHistory,
        SteamWorkshopService workshop, ServerGroupService groups, WebApiService webApi,
        ScheduledTaskService scheduler, RemoteMachineService remoteMachines,
        CrashPredictionService crashPrediction)
    {
        _config          = config;
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
        _groups          = groups;
        _webApi          = webApi;
        _scheduler       = scheduler;
        _remoteMachines  = remoteMachines;
        _crashPrediction = crashPrediction;
        Settings         = settings;
        Dashboard        = new DashboardViewModel(metrics, Servers);

        manager.StatusChanged += (_, _) =>
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(RunningCount));
                OnPropertyChanged(nameof(StoppedCount));
                _tray.SetStatus(RunningCount, TotalServers);
            });

        Servers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SortedServers));
        LoadServers();

        // Wire up Discord bot callbacks
        _bot.GetServers    = () => Servers.Select(v => v.Server);
        _bot.StartServer   = async id => { var vm = FindServer(id); if (vm != null) await vm.StartCommand.ExecuteAsync(null); };
        _bot.StopServer    = async id => { var vm = FindServer(id); if (vm != null) await vm.StopCommand.ExecuteAsync(null); };
        _bot.RestartServer = async id => { var vm = FindServer(id); if (vm != null) await vm.RestartCommand.ExecuteAsync(null); };
        _bot.UpdateServer  = async id => { var vm = FindServer(id); if (vm != null) await vm.UpdateCommand.ExecuteAsync(null); };
        _bot.BackupServer  = async id => { var vm = FindServer(id); if (vm != null) await vm.CreateBackupCommand.ExecuteAsync(null); };
        _bot.SendCmd       = async (id, cmd) => await manager.SendCommandAsync(id, cmd);

        // Start bot if already configured
        _bot.ApplySettings(notifications.Settings);

        // Wire Scheduled Task callbacks
        _scheduler.GetServers   = () => Servers.Select(v => v.Server);
        _scheduler.UpdateServer = async id => { var vm = FindServer(id); if (vm != null) await vm.UpdateCommand.ExecuteAsync(null); };

        // Wire Web API callbacks
        _webApi.GetServers    = () => Servers.Select(v => v.Server);
        _webApi.StartServer   = async id => { var vm = FindServer(id); if (vm != null) await vm.StartCommand.ExecuteAsync(null); };
        _webApi.StopServer    = async id => { var vm = FindServer(id); if (vm != null) await vm.StopCommand.ExecuteAsync(null); };
        _webApi.RestartServer = async id => { var vm = FindServer(id); if (vm != null) await vm.RestartCommand.ExecuteAsync(null); };
        _webApi.UpdateServer  = async id => { var vm = FindServer(id); if (vm != null) await vm.UpdateCommand.ExecuteAsync(null); };
        _webApi.BackupServer  = async id => { var vm = FindServer(id); if (vm != null) await vm.CreateBackupCommand.ExecuteAsync(null); };
        _webApi.SendCmd       = async (id, cmd) => await manager.SendCommandAsync(id, cmd);
        _webApi.GetMetrics    = () => metrics.Current;

        // Wire RemoteMachineService
        foreach (var machine in _remoteMachines.Machines)
            RemoteMachines.Add(new MachineViewModel(machine, _remoteMachines));

        _remoteMachines.MachineUpdated += OnMachineUpdated;
        _remoteMachines.MachinesChanged += OnMachinesChanged;

        // Wire CrashPredictionService — snapshot on UI thread so background thread gets a safe copy
        _crashPrediction.GetRunningServers = () =>
            WpfApplication.Current?.Dispatcher?.Invoke(() =>
                Servers.Where(v => v.Server.Status == ServerStatus.Running)
                       .Select(v => (v.Server.Id, v.Server.DisplayName))
                       .ToList()
                       .AsEnumerable()
            ) ?? [];
        _crashPrediction.PredictionRaised += OnCrashPrediction;
    }

    private void OnMachineUpdated(string machineId, List<Services.RemoteServerInfo> servers)
    {
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            var vm = RemoteMachines.FirstOrDefault(m => m.Definition.Id == machineId);
            if (vm == null) return;
            vm.IsOnline = servers.Count > 0 || _remoteMachines.IsOnline(machineId);
            vm.UpdateServers(servers);
        });
    }

    private void OnMachinesChanged()
    {
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            RemoteMachines.Clear();
            foreach (var machine in _remoteMachines.Machines)
                RemoteMachines.Add(new MachineViewModel(machine, _remoteMachines));
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
        foreach (var srv in _config.LoadServers())
        {
            srv.Status = ServerStatus.Stopped;
            var vm = MakeVm(srv);
            Servers.Add(vm);
            if (srv.AutoStart)
                _ = Task.Run(() => vm.StartCommand.ExecuteAsync(null))
                        .ContinueWith(t => Console.WriteLine($"[WGS] AutoStart failed for {srv.DisplayName}: {t.Exception?.InnerException?.Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
        }
        SelectedServer = Servers.FirstOrDefault();
        RefreshCounts();
        _tray.SetStatus(RunningCount, TotalServers);
    }

    private ServerViewModel? FindServer(string id) => Servers.FirstOrDefault(v => v.Server.Id == id);

    private ServerViewModel MakeVm(GameServer srv)
        => new(srv, _manager, _steamCmd, _backup, _notifications, _perfMonitor, _config, _mods,
               _configEditor, _playerStats, _perfHistory, _workshop, _scheduler);

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
            ShowDashboard = false;
            ShowSupport   = false;
        }
    }
    partial void OnSortModeChanged(string value) => OnPropertyChanged(nameof(SortedServers));
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
        Servers.Add(vm);
        SelectedServer = vm;
        ShowAddDialog  = false;
        Save();
        RefreshCounts();
    }

    [RelayCommand]
    private async Task RemoveServerAsync(ServerViewModel? vm)
    {
        if (vm == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Remove server \"{vm.Server.DisplayName}\"?\n\nFiles will not be deleted from disk.",
            "Confirm removal", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        if (vm.IsRunning) await vm.StopCommand.ExecuteAsync(null);
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
        ShowDashboard    = true;
        ShowSettingsPage = false;
        ShowSupport      = false;
    }

    [RelayCommand]
    private void OpenSupport()
    {
        ShowSupport      = true;
        ShowDashboard    = false;
        ShowSettingsPage = false;
    }

    [RelayCommand]
    private void OpenPluginCreator()
    {
        var vm  = new PluginCreatorViewModel();
        var win = new PluginCreatorView(vm);
        win.Owner = WpfApplication.Current.MainWindow;
        win.ShowDialog();
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
        var win = new WGS.Views.SettingsWindow(Settings);
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
