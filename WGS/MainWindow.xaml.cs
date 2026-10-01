using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
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

        // Supply the correct maximized position/size (monitor work area) via WM_GETMINMAXINFO.
        // Diagnosed and fixed by CoDrift (GitHub issue #15) after three failed attempts here —
        // see WndProc below for exactly what those attempts got wrong and why.
        SourceInitialized += (_, _) =>
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
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
        // Maximized size comes from the WM_GETMINMAXINFO hook (WndProc), however Maximized is reached.
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

    // ── Maximize fix (GitHub issue #15) — WM_GETMINMAXINFO hook ─────────────────
    // Three earlier attempts here all got this wrong in different ways:
    //   v1.5.24 — handled WM_GETMINMAXINFO but set ptMaxPosition to exactly the work-area offset
    //             (zero overhang). Windows' own default actually sets ptMaxPosition NEGATIVE on
    //             purpose: the window's invisible resize-frame is meant to overhang the work area
    //             by the frame thickness so the visible client area fills it exactly. Zeroing that
    //             out made the frame visible as a gap on all four sides (worse than before: v1.5.23
    //             only gapped two edges).
    //   v1.5.25 — same WM_GETMINMAXINFO approach via WPF's CompositionTarget transform instead of
    //             raw Win32 pixels; didn't address the frame-overhang issue above, so no change.
    //   v1.5.26 — abandoned WM_GETMINMAXINFO for forcing Width/Height directly in StateChanged.
    //             Logged on the reporter's actual RDP session: this produced an oversized window
    //             (covering the taskbar, not leaving a gap) AND corrupted RestoreBounds, so
    //             un-maximizing no longer returned to the window's previous size/position.
    // Root-caused and fixed by CoDrift (reporter of #15) with real position/size logging from both
    // a local run and the original RDP repro. The fix: read Windows' own default negative
    // ptMaxPosition (the frame thickness it already wants to hide off-screen) before overwriting it,
    // then reproduce that same overhang relative to the correct monitor's work area instead of
    // removing it.
    const int WM_GETMINMAXINFO = 0x0024;
    const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (mon != IntPtr.Zero && GetMonitorInfo(mon, ref mi))
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                // Windows' default ptMaxPosition is (-frame, -frame): the window overhangs the
                // target area by the resize-frame thickness so the client area fills it exactly.
                int fx = Math.Max(0, -mmi.ptMaxPosition.X);
                int fy = Math.Max(0, -mmi.ptMaxPosition.Y);

                mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left - fx;
                mmi.ptMaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top - fy;
                mmi.ptMaxSize.X = (mi.rcWork.Right - mi.rcWork.Left) + 2 * fx;
                mmi.ptMaxSize.Y = (mi.rcWork.Bottom - mi.rcWork.Top) + 2 * fy;

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }
}
