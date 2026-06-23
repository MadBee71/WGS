using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WGS.Models;
using WGS.Services;

namespace WGS.ViewModels;

public partial class RemoteServerViewModel : ObservableObject, IDisposable
{
    private readonly RemoteMachineService _remoteMachines;
    private readonly string               _machineId;
    private System.Timers.Timer?          _pollTimer;
    private int                           _logOffset;
    private bool                          _isActive;
    private readonly SemaphoreSlim        _pollGate = new(1, 1);
    private readonly object               _startLock = new();

    public string   ServerId    => Info.Id;
    public string   DisplayName => Info.DisplayName;
    public string   GameId      => Info.GameId;
    public string   MachineName { get; }

    public string GameImageUrl
    {
        get
        {
            var plugin = Games.GameRegistry.Get(Info.GameId);
            if (plugin?.GameStoreAppId > 0)
                return $"https://cdn.akamai.steamstatic.com/steam/apps/{plugin.GameStoreAppId}/capsule_sm_120.jpg";
            if (plugin != null && ServerViewModel.LocalGameImages.TryGetValue(plugin.GameId, out var localImage))
                return $"pack://application:,,,/{localImage}";
            return "pack://application:,,,/no_image.png"; // games with no Steam store page and no local logo
        }
    }

    public string GameName
    {
        get
        {
            var plugin = Games.GameRegistry.Get(Info.GameId);
            return plugin?.GameName ?? Info.GameId;
        }
    }

    public ObservableCollection<ConsoleMessage> Log { get; } = [];

    [ObservableProperty] private string _status      = "Stopped";
    [ObservableProperty] private string _consoleInput = string.Empty;

    public string StatusColor => Status switch
    {
        "Running"      => "#3FB950",
        "Starting"     => "#58A6FF",
        "Stopping"     => "#58A6FF",
        "Stopped"      => "#8B949E",
        "Installing"   => "#58A6FF",
        "Updating"     => "#58A6FF",
        "Error"        => "#F85149",
        "NotInstalled" => "#8B949E",
        _              => "#8B949E",
    };

    public bool CanStart => Status is "Stopped" or "Error" or "NotInstalled";
    public bool CanStop  => Status is "Running" or "Starting";

    public RemoteServerInfo Info { get; private set; }

    public RemoteServerViewModel(string machineId, string machineName,
        RemoteServerInfo info, RemoteMachineService remoteMachines)
    {
        _machineId      = machineId;
        MachineName     = machineName;
        Info            = info;
        _status         = info.Status;
        _remoteMachines = remoteMachines;
    }

    public void UpdateInfo(RemoteServerInfo info)
    {
        Info   = info;
        Status = info.Status;
    }

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    public void StartPolling()
    {
        lock (_startLock)
        {
            if (_isActive) return;
            _isActive  = true;
            _logOffset = 0;
            WpfApplication.Current?.Dispatcher?.Invoke(() => Log.Clear());
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            _pollTimer = new System.Timers.Timer(2000);
            _pollTimer.Elapsed += async (_, _) => await PollAsync();
            _pollTimer.AutoReset = true;
            _pollTimer.Start();
        }
        _ = PollAsync();
    }

    public void StopPolling()
    {
        lock (_startLock)
        {
            _isActive = false;
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            _pollTimer = null;
        }
    }

    private async Task PollAsync()
    {
        if (!_isActive) return;
        if (!await _pollGate.WaitAsync(0)) return; // skip if previous poll still running
        try
        {
            if (!_isActive) return;
            var chunk = await _remoteMachines.GetLogAsync(_machineId, ServerId, _logOffset);
            if (chunk == null || chunk.Lines.Count == 0) return;
            _logOffset = chunk.NextOffset;
            var app = WpfApplication.Current;
            if (app == null) return;
            app.Dispatcher?.Invoke(() =>
            {
                for (int i = 0; i < chunk.Lines.Count; i++)
                {
                    var type = Enum.TryParse<ConsoleMessageType>(chunk.Types.ElementAtOrDefault(i), out var t)
                        ? t : ConsoleMessageType.Info;
                    Log.Add(new ConsoleMessage { Text = chunk.Lines[i], Type = type });
                }
            });
        }
        finally { _pollGate.Release(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task StartAsync()
    {
        if (await _remoteMachines.StartServer(_machineId, ServerId))
            Status = "Starting";
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (await _remoteMachines.StopServer(_machineId, ServerId))
            Status = "Stopping";
    }

    [RelayCommand]
    private async Task RestartAsync()
    {
        if (await _remoteMachines.RestartServer(_machineId, ServerId))
            Status = "Starting";
    }

    [RelayCommand]
    private async Task SendConsoleCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(ConsoleInput)) return;
        var cmd = ConsoleInput;
        ConsoleInput = string.Empty;
        Log.Add(new ConsoleMessage { Text = "> " + cmd, Type = ConsoleMessageType.Input });
        await _remoteMachines.SendCommandAsync(_machineId, ServerId, cmd);
    }

    [RelayCommand]
    private void ClearConsole() => Log.Clear();

    public void Dispose() => StopPolling();
}
