using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WGS.Models;
using WGS.Services;

namespace WGS.ViewModels;

public partial class MachineViewModel : ObservableObject
{
    private readonly RemoteMachineService _remoteMachineService;

    public MachineDefinition Definition { get; }
    public ObservableCollection<RemoteServerInfo> Servers { get; } = new();

    [ObservableProperty] private bool _isOnline;

    public string StatusText    => IsOnline ? "Online" : "Offline";
    public string AccentColor   => Definition.Color;
    public string MachineName   => Definition.Name;
    public string MachineUrl    => Definition.Url;

    public MachineViewModel(MachineDefinition definition, RemoteMachineService remoteMachineService)
    {
        Definition            = definition;
        _remoteMachineService = remoteMachineService;
    }

    partial void OnIsOnlineChanged(bool value) => OnPropertyChanged(nameof(StatusText));

    public void UpdateServers(List<RemoteServerInfo> servers)
    {
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            Servers.Clear();
            foreach (var s in servers) Servers.Add(s);
        });
    }

    [RelayCommand]
    private async Task StartServerAsync(RemoteServerInfo? server)
    {
        if (server == null) return;
        await _remoteMachineService.StartServer(Definition.Id, server.Id);
    }

    [RelayCommand]
    private async Task StopServerAsync(RemoteServerInfo? server)
    {
        if (server == null) return;
        await _remoteMachineService.StopServer(Definition.Id, server.Id);
    }

    [RelayCommand]
    private async Task RestartServerAsync(RemoteServerInfo? server)
    {
        if (server == null) return;
        await _remoteMachineService.RestartServer(Definition.Id, server.Id);
    }
}
