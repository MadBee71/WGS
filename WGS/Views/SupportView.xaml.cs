using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace WGS.Views;

public partial class SupportView : System.Windows.Controls.UserControl
{
    public static readonly RoutedUICommand OpenUrlCommand = new("OpenUrl", "OpenUrl", typeof(SupportView));

    public SupportView()
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(OpenUrlCommand, OnOpenUrl));
    }

    private void KoFiButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://ko-fi.com/madbee71");
    }

    private static void OnOpenUrl(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string url)
            OpenUrl(url);
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
