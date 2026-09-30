using System.Runtime.InteropServices;
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

    // ── Correct maximize sizing for a WindowStyle=None window ──────────────────
    // SystemParameters.WorkArea (formerly used in MaximizeClick below) is only an approximation —
    // it assumes the primary monitor and doesn't always match the real work area under RDP/
    // non-standard DPI, which is exactly what left gaps around the maximized window (reported:
    // GitHub issue #15, Windows Server 2022 over RDP, 100% scaling). Handling WM_GETMINMAXINFO
    // directly via the monitor Win32 actually reports for this window is the standard fix (the
    // same mechanism WPF's own WindowChrome uses internally) — correct regardless of monitor/DPI.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var info = new MONITORINFO();
                info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(monitor, ref info);

                var workArea   = info.rcWork;
                var monitorArea = info.rcMonitor;

                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                // Position/size are relative to the monitor's own top-left, not the virtual
                // desktop — offsetting by monitorArea.Left/Top is what makes this correct on a
                // non-primary monitor too, not just the primary one SystemParameters assumed.
                mmi.ptMaxPosition.X = workArea.Left - monitorArea.Left;
                mmi.ptMaxPosition.Y = workArea.Top  - monitorArea.Top;
                mmi.ptMaxSize.X     = workArea.Right  - workArea.Left;
                mmi.ptMaxSize.Y     = workArea.Bottom - workArea.Top;
                mmi.ptMaxTrackSize.X = mmi.ptMaxSize.X;
                mmi.ptMaxTrackSize.Y = mmi.ptMaxSize.Y;
                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private const int MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
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
        // Correct sizing (monitor- and DPI-aware) now comes from the WM_GETMINMAXINFO hook above —
        // no manual MaxWidth/Height/Left/Top needed, and no longer just an approximation.
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
