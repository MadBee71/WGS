namespace WGS.Views;

public partial class OnboardingDialog : System.Windows.Window
{
    public bool DontShowAgain { get; private set; }

    public OnboardingDialog()
    {
        InitializeComponent();
    }

    private void GotItClick(object sender, System.Windows.RoutedEventArgs e)
    {
        DontShowAgain = DontShowAgainCheck.IsChecked == true;
        Close();
    }
}
