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

    // The console list is virtualized, and its ScrollViewer lives inside the ItemsControl's template
    // (so it isn't a generated field any more) — look it up from the template when needed.
    private ScrollViewer? LogScroller
    {
        get
        {
            // LogList is still null while InitializeComponent runs (AutoScrollToggle's Checked event
            // fires during XAML load), and the template isn't applied until first layout.
            var list = LogList;
            if (list == null) return null;
            try { return list.Template?.FindName("PART_LogScroller", list) as ScrollViewer; }
            catch (InvalidOperationException) { return null; }
        }
    }

    private bool _scrollPending;

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Coalesce: a burst of lines queues one scroll, not one per line. Re-check _autoScroll when it
        // runs so a scroll the user did in the meantime isn't undone.
        if (!_autoScroll || _scrollPending) return;
        _scrollPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            _scrollPending = false;
            if (_autoScroll) LogScroller?.ScrollToBottom();
        });
    }

    // User-scroll detection is based on the user's own input (wheel / scrollbar), not on
    // ScrollChanged: with virtualization the extent estimate shifts while scrolling, so
    // ScrollChanged can't tell a user scroll from the list re-measuring itself.
    private void LogList_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (e.Delta > 0) SetAutoScroll(false);   // scrolling up: stop following the log immediately
        else SyncAutoScrollToPosition();         // scrolling down: resume following if we reached the bottom
    }

    private void LogList_ScrollBarScroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
    {
        SetAutoScroll(false);
        SyncAutoScrollToPosition();              // dragged/clicked back to the bottom → resume following
    }

    private void SetAutoScroll(bool value)
    {
        if (_autoScroll == value) return;
        _autoScroll = value;
        AutoScrollToggle.IsChecked = value;
    }

    private void SyncAutoScrollToPosition()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            var sv = LogScroller;
            if (sv == null) return;
            SetAutoScroll(sv.VerticalOffset >= sv.ScrollableHeight - 2);
        });
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

    private void PluginsTab_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ServerViewModel vm)
            vm.RefreshSourceModCommand.Execute(null);
    }
}
