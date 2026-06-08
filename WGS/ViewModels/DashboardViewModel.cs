using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using WGS.Models;
using WGS.Services;

namespace WGS.ViewModels;

public partial class DashboardViewModel : BaseViewModel, IDisposable // #2: IDisposable
{
    private readonly SystemMetricsService  _metrics;
    private readonly NetworkMonitorService _network;
    private readonly ObservableCollection<ServerViewModel> _servers;

    [ObservableProperty] private float  _cpuPercent;
    [ObservableProperty] private long   _ramUsedMb;
    [ObservableProperty] private long   _ramTotalMb;
    [ObservableProperty] private double _ramPercent;
    [ObservableProperty] private IReadOnlyList<DriveStats> _drives = [];
    [ObservableProperty] private int    _serverCount;
    [ObservableProperty] private int    _onlineCount;
    [ObservableProperty] private int    _stoppedCount;
    [ObservableProperty] private string _networkIn  = "—";
    [ObservableProperty] private string _networkOut = "—";

    public double RamUsedGb  => Math.Round(RamUsedMb  / 1024.0, 1);
    public double RamTotalGb => Math.Round(RamTotalMb / 1024.0, 1);

    public DashboardViewModel(SystemMetricsService metrics, NetworkMonitorService network,
        ObservableCollection<ServerViewModel> servers)
    {
        _metrics = metrics;
        _network = network;
        _servers = servers;

        _metrics.MetricsUpdated    += OnMetricsUpdated;
        _network.Updated           += OnNetworkUpdated;
        _servers.CollectionChanged += OnServersChanged; // #2: nimetty — voidaan derekisteröidä

        OnMetricsUpdated();
        UpdateServerCounts();
    }

    // #2: Derekisteröi kaikki event-handlerit
    public void Dispose()
    {
        _metrics.MetricsUpdated    -= OnMetricsUpdated;
        _network.Updated           -= OnNetworkUpdated;
        _servers.CollectionChanged -= OnServersChanged;
    }

    private void OnServersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateServerCounts();

    private void OnMetricsUpdated()
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            CpuPercent = _metrics.CpuPercent;
            RamUsedMb  = _metrics.RamUsedMb;
            RamTotalMb = _metrics.RamTotalMb;
            RamPercent = RamTotalMb > 0 ? Math.Round((double)RamUsedMb / RamTotalMb * 100.0, 1) : 0;
            Drives     = _metrics.Drives;

            OnPropertyChanged(nameof(RamUsedGb));
            OnPropertyChanged(nameof(RamTotalGb));

            UpdateServerCounts();
        });
    }

    private void OnNetworkUpdated()
    {
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            NetworkIn  = NetworkMonitorService.FormatSpeed(_network.CurrentBytesInPerSec);
            NetworkOut = NetworkMonitorService.FormatSpeed(_network.CurrentBytesOutPerSec);
        });
    }

    private void UpdateServerCounts()
    {
        ServerCount  = _servers.Count;
        OnlineCount  = _servers.Count(s => s.Server.Status == ServerStatus.Running);
        StoppedCount = _servers.Count(s => s.Server.Status != ServerStatus.Running);
    }
}
