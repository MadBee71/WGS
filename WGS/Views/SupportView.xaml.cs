using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace WGS.Views;

public partial class SupportView : System.Windows.Controls.UserControl
{
    public SupportView()
    {
        InitializeComponent();
    }

    private void KoFiButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://ko-fi.com/madbee71");
    }

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/MadBee71/WGS");
    }

    private void GitHubLink_Click(object sender, MouseButtonEventArgs e)
    {
        OpenUrl("https://github.com/MadBee71/WGS");
    }

    private void KoFiLink_Click(object sender, MouseButtonEventArgs e)
    {
        OpenUrl("https://ko-fi.com/madbee71");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }
}
