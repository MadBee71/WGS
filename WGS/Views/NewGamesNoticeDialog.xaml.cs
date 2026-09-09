namespace WGS.Views;

public partial class NewGamesNoticeDialog : System.Windows.Window
{
    public bool DontShowAgain { get; private set; }

    public NewGamesNoticeDialog()
    {
        InitializeComponent();
    }

    private void GotItClick(object sender, System.Windows.RoutedEventArgs e)
    {
        DontShowAgain = DontShowAgainCheck.IsChecked == true;
        Close();
    }
}
