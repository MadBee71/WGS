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
    private readonly ServerManagerService _manager;
    private readonly SteamCmdService _steamCmd;
    private readonly BackupService _backup;
    private readonly NotificationService _notifications;
    private readonly PerformanceMonitorService _perfMonitor;
    private readonly ConfigService _config;
    private RconService? _rcon;
    private readonly SemaphoreSlim _rconLock  = new(1, 1);
    private readonly object        _perfLock  = new();

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
    [ObservableProperty] private string _consoleFilter = string.Empty;

    private System.Timers.Timer? _perfTimer;

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
    public bool HasRcon      => Plugin?.HasRcon == true;

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
        ConfigService config)
    {
        Server         = server;
        Plugin         = GameRegistry.Get(server.GameId);
        _manager       = manager;
        _steamCmd      = steamCmd;
        _backup        = backup;
        _notifications = notifications;
        _perfMonitor   = perfMonitor;
        _config        = config;

        _manager.LogReceived      += OnLogReceived;
        _manager.StatusChanged    += OnStatusChanged;
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
            StartPerfMonitoring();
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
            await _manager.StopAsync(Server);
            StopPerfMonitoring();
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

        // Ask for Steam Guard code upfront (only when using authenticated login)
        string? steamGuardCode = null;
        if (login != null)
        {
            steamGuardCode = await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
            {
                var dlg = new WGS.Views.SteamGuardDialog
                {
                    Owner = WpfApplication.Current.MainWindow
                };
                return dlg.ShowDialog() == true ? dlg.Code : null;
            }).Task;

            if (steamGuardCode == null)
            {
                AppendLog("[WGS] Install cancelled.", ConsoleMessageType.System);
                IsInstalling = false;
                Server.Status = ServerStatus.NotInstalled;
                RefreshStatus();
                return;
            }
        }

        try
        {
            await _steamCmd.InstallOrUpdateAsync(Plugin.SteamAppId, Server.InstallPath, login, password, steamGuardCode);
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
        WpfApplication.Current?.Dispatcher?.Invoke(() => { CpuPercent = 0; MemoryMb = 0; });
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
        _manager.LogReceived      -= OnLogReceived;
        _manager.StatusChanged    -= OnStatusChanged;
        _steamCmd.OutputReceived  -= OnSteamOutput;
        _steamCmd.ProgressChanged -= OnSteamProgress;

        StopPerfMonitoring();

        _rconLock.Wait();
        try { _rcon?.Dispose(); _rcon = null; }
        finally { _rconLock.Release(); }

        _rconLock.Dispose();
    }
}
