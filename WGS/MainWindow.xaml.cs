using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        var config = App.Services.GetRequiredService<Services.ConfigService>();
        if (config.HasSeenOnboarding) return;

        var dlg = new Views.OnboardingDialog { Owner = this };
        dlg.ShowDialog();
        if (dlg.DontShowAgain)
        {
            config.HasSeenOnboarding = true;
            config.Save();
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            return;
        }

        // This app has no app.manifest, so it's only "System DPI Aware" (.NET/WPF's default),
        // not Per-Monitor — confirmed by research, not assumed. That means Win32 APIs like
        // GetMonitorInfo return physical pixels that do NOT reliably match what WindowStyle=None's
        // Max* properties expect (a WM_GETMINMAXINFO hook using those physical pixels directly was
        // tried and made GitHub issue #15 *worse*, turning a two-edge gap into a four-edge one —
        // exactly the symptom of a DPI unit mismatch). Converting through WPF's own
        // CompositionTarget.TransformFromDevice matrix is correct regardless of DPI-awareness mode,
        // since WPF computes that matrix for this exact window itself.
        var hwnd      = new WindowInteropHelper(this).Handle;
        var screen    = System.Windows.Forms.Screen.FromHandle(hwnd);
        var workArea  = screen.WorkingArea;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
                        ?? System.Windows.Media.Matrix.Identity;
        var topLeft   = transform.Transform(new System.Windows.Point(workArea.Left, workArea.Top));
        var size      = transform.Transform(new System.Windows.Vector(workArea.Width, workArea.Height));

        MaxWidth    = size.X;
        MaxHeight   = size.Y;
        Left        = topLeft.X;
        Top         = topLeft.Y;
        WindowState = WindowState.Maximized;
    }

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
