using WGS.ViewModels;

namespace WGS.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncPassword();
    }

    private void SyncPassword()
    {
        if (DataContext is SettingsViewModel vm)
        {
            SteamPassBox.Password = vm.SteamPassword;
            SmtpPasswordBox.Password = vm.SmtpPassword;
        }
    }

    private void SteamPassBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.SteamPassword = SteamPassBox.Password;
    }

    private void SmtpPasswordBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.SmtpPassword = SmtpPasswordBox.Password;
    }
}
