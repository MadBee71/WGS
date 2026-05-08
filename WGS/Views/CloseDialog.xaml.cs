using System.Windows;

namespace WGS.Views;

public partial class CloseDialog : Window
{
    public enum CloseResult { Minimize, Close, Cancel }
    public CloseResult Result { get; private set; } = CloseResult.Cancel;

    public CloseDialog() => InitializeComponent();

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        Result = CloseResult.Minimize;
        Close();
    }

    private void CloseAppClick(object sender, RoutedEventArgs e)
    {
        Result = CloseResult.Close;
        Close();
    }
}
