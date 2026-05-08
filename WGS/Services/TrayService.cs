using System.Drawing;
using System.Windows.Forms;

namespace WGS.Services;

public class TrayService : IDisposable
{
    private NotifyIcon? _tray;
    private readonly ServerManagerService _manager;

    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    public TrayService(ServerManagerService manager)
    {
        _manager = manager;
        Initialize();
    }

    private void Initialize()
    {
        _tray = new NotifyIcon
        {
            Text    = "Windows Game Server",
            Visible = true,
            Icon    = CreateIcon(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open WGS", null,    (_, _) => ShowWindowRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",     null,    (_, _) => ExitRequested?.Invoke());

        _tray.ContextMenuStrip  = menu;
        _tray.DoubleClick += (_, _) => ShowWindowRequested?.Invoke();
    }

    public void SetStatus(int running, int total)
    {
        if (_tray != null)
            _tray.Text = $"WGS — {running}/{total} servers running";
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        => _tray?.ShowBalloonTip(4000, title, message, icon);

    private static Icon CreateIcon()
    {
        try
        {
            var sri = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/WindowsGameServer;component/favicon.ico"));
            if (sri != null) return new Icon(sri.Stream);
        }
        catch { }
        // Fallback: plain icon
        var bmp = new Bitmap(16, 16);
        using (bmp)
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(13, 17, 23));
            g.FillRectangle(new SolidBrush(Color.FromArgb(88, 166, 255)), 3, 3, 10, 10);
        }
        var hicon = bmp.GetHicon();
        return Icon.FromHandle(hicon);
    }

    public void Dispose()
    {
        _tray?.Dispose();
    }
}
