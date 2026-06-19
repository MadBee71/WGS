using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using WGS.ViewModels;

namespace WGS.Views;

public partial class ServerDetailView : System.Windows.Controls.UserControl
{
    private bool _autoScroll = true;
    private INotifyCollectionChanged? _hookedLog;

    public ServerDetailView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Unhook previous log
        if (_hookedLog != null)
        {
            _hookedLog.CollectionChanged -= OnLogChanged;
            _hookedLog = null;
        }

        if (e.NewValue is ServerViewModel vm)
        {
            _hookedLog = vm.FilteredLog;
            _hookedLog.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_autoScroll)
            Dispatcher.BeginInvoke(() => LogScroller?.ScrollToBottom());
    }

    private void LogScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // If the user scrolled (not caused by content growing), update auto-scroll state
        if (e.ExtentHeightChange == 0)
        {
            // User manually scrolled — check if they're at the bottom
            _autoScroll = LogScroller.VerticalOffset >= LogScroller.ScrollableHeight - 2;
            AutoScrollToggle.IsChecked = _autoScroll;
        }
    }

    private void AutoScrollToggle_Changed(object sender, RoutedEventArgs e)
    {
        _autoScroll = AutoScrollToggle.IsChecked == true;
        if (_autoScroll)
            LogScroller?.ScrollToBottom();
    }

    private void AddScheduleTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ServerViewModel vm) return;

        var dlg = new AddScheduleTaskDialog(vm.Server.QuickCommands) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true || dlg.Result == null) return;

        vm.AddScheduledTaskCommand.Execute(dlg.Result);
    }
}
