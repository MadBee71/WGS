using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using WGS.ViewModels;

namespace WGS;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
        try
        {
            var sri = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/WindowsGameServer;component/favicon.ico"));
            if (sri != null)
                using (sri.Stream)
                    Icon = BitmapFrame.Create(sri.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch { }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // Stops the click from bubbling up to TitleBar_MouseDown — DragMove() captures the mouse on
    // button-down and swallows the matching button-up, so UpdateBadge_Click below would otherwise
    // never fire no matter how precisely you click without moving the mouse.
    private void UpdateBadge_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void UpdateBadge_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
            vm.PerformUpdateCommand.Execute(null);
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new WGS.Views.CloseDialog { Owner = this };
        dlg.ShowDialog();
        if (dlg.Result == WGS.Views.CloseDialog.CloseResult.Close)
            System.Windows.Application.Current.Shutdown();
        else if (dlg.Result == WGS.Views.CloseDialog.CloseResult.Minimize)
        {
            Hide();
            WindowState = WindowState.Minimized;
        }
    }
}
