namespace WGS.Views;

public partial class RemoteServerDetailView : System.Windows.Controls.UserControl
{
    private bool _autoScroll = true;

    public RemoteServerDetailView() => InitializeComponent();

    private void LogScroller_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange == 0)
            _autoScroll = LogScroller.VerticalOffset >= LogScroller.ScrollableHeight - 2;
        else if (_autoScroll)
            LogScroller.ScrollToBottom();
    }
}
