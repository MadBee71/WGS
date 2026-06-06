using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WGS.Services;
using WpfMsgBox       = System.Windows.MessageBox;
using WpfMsgBoxButton = System.Windows.MessageBoxButton;
using WpfMsgBoxImage  = System.Windows.MessageBoxImage;

namespace WGS.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly NotificationService _notifications;
    private readonly ConfigService       _config;
    private readonly SteamCmdService     _steamCmd;
    private readonly DiscordBotService   _bot;
    private readonly WebApiService       _webApi;

    // ── Webhook notifications ─────────────────────────────────────────────────
    [ObservableProperty] private bool   _discordEnabled;
    [ObservableProperty] private string _discordWebhookUrl = string.Empty;
    [ObservableProperty] private bool   _notifyOnStart     = true;
    [ObservableProperty] private bool   _notifyOnStop      = true;
    [ObservableProperty] private bool   _notifyOnCrash     = true;
    [ObservableProperty] private bool   _notifyOnUpdate    = true;

    // ── Discord remote-control bot ────────────────────────────────────────────
    [ObservableProperty] private bool   _botEnabled;
    [ObservableProperty] private string _botToken        = string.Empty;
    [ObservableProperty] private string _botChannelId    = string.Empty;
    [ObservableProperty] private string _botPrefix       = "!";
    [ObservableProperty] private string _botAllowedUsers = string.Empty;
    [ObservableProperty] private string _botStatus       = string.Empty;

    // ── Web API ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _webApiEnabled;
    [ObservableProperty] private int    _webApiPort    = 8765;
    [ObservableProperty] private string _webApiToken   = string.Empty;
    [ObservableProperty] private string _webApiStatus  = string.Empty;
    public bool WebApiStatusIsWarning => WebApiStatus.StartsWith("⚠");

    // ── Slave / Remote Control ────────────────────────────────────────────────
    [ObservableProperty] private bool   _slaveMode;
    [ObservableProperty] private string _slaveName = "This Machine";

    public string SlaveConnectionUrl => BuildSlaveUrl();

    // ── Crash Prediction ──────────────────────────────────────────────────────
    [ObservableProperty] private bool   _crashPredictionDiscord;

    // ── General / paths ───────────────────────────────────────────────────────
    [ObservableProperty] private string _defaultInstallRoot  = string.Empty;
    [ObservableProperty] private string _backupPath          = string.Empty;
    [ObservableProperty] private string _steamCmdPath        = string.Empty;
    [ObservableProperty] private string _steamLogin          = string.Empty;
    [ObservableProperty] private string _steamPassword       = string.Empty;
    [ObservableProperty] private bool   _startWithWindows;

    private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WindowsGameServer";

    private static bool GetStartWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) != null;
    }

    private static void SetStartWithWindows(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)!;
        if (enable)
        {
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                      ?? System.IO.Path.Combine(AppContext.BaseDirectory, "WindowsGameServer.exe");
            key.SetValue(AppName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    public SettingsViewModel(NotificationService notifications, ConfigService config,
                             SteamCmdService steamCmd, DiscordBotService bot, WebApiService webApi)
    {
        _notifications = notifications;
        _config        = config;
        _steamCmd      = steamCmd;
        _bot           = bot;
        _webApi        = webApi;
        _bot.StatusChanged += msg => WpfApplication.Current?.Dispatcher?.Invoke(() => BotStatus = msg);
        Load();
    }

    private void Load()
    {
        var s = _notifications.Settings;
        DiscordEnabled     = s.DiscordEnabled;
        DiscordWebhookUrl  = s.DiscordWebhookUrl;
        NotifyOnStart      = s.NotifyOnStart;
        NotifyOnStop       = s.NotifyOnStop;
        NotifyOnCrash      = s.NotifyOnCrash;
        NotifyOnUpdate     = s.NotifyOnUpdate;
        BotEnabled         = s.BotEnabled;
        BotToken           = s.BotToken;
        BotChannelId       = s.BotChannelId;
        BotPrefix          = string.IsNullOrEmpty(s.BotPrefix) ? "!" : s.BotPrefix;
        BotAllowedUsers    = s.BotAllowedUsers;
        DefaultInstallRoot = _config.DefaultInstallRoot;
        BackupPath         = _config.BackupPath;
        SteamCmdPath       = System.IO.Path.Combine(_config.AppDataPath, "steamcmd");
        SteamLogin         = _config.SteamLogin;
        SteamPassword      = _config.SteamPassword;
        BotStatus          = _bot.IsRunning ? "🟢 Running" : "⚫ Stopped";
        WebApiEnabled            = _config.WebApiEnabled;
        WebApiPort               = _config.WebApiPort;
        WebApiToken              = _config.WebApiToken;
        WebApiStatus             = BuildWebApiStatus();
        SlaveMode                = _config.SlaveMode;
        SlaveName                = _config.SlaveName;
        CrashPredictionDiscord   = _config.CrashPredictionDiscord;
        StartWithWindows         = GetStartWithWindows();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var s = _notifications.Settings;
        s.DiscordEnabled    = DiscordEnabled;
        s.DiscordWebhookUrl = DiscordWebhookUrl;
        s.NotifyOnStart     = NotifyOnStart;
        s.NotifyOnStop      = NotifyOnStop;
        s.NotifyOnCrash     = NotifyOnCrash;
        s.NotifyOnUpdate    = NotifyOnUpdate;
        s.BotEnabled        = BotEnabled;
        s.BotToken          = BotToken;
        s.BotChannelId      = BotChannelId;
        s.BotPrefix         = BotPrefix;
        s.BotAllowedUsers   = BotAllowedUsers;
        _notifications.Save();

        _config.DefaultInstallRoot     = DefaultInstallRoot;
        _config.BackupPath             = BackupPath;
        _config.SteamLogin             = SteamLogin;
        _config.SteamPassword          = SteamPassword;
        _config.WebApiEnabled          = WebApiEnabled;
        _config.WebApiPort             = WebApiPort;
        _config.WebApiToken            = string.IsNullOrWhiteSpace(WebApiToken) ? Guid.NewGuid().ToString("N") : WebApiToken;
        WebApiToken                    = _config.WebApiToken; // reflect generated token back to UI
        _config.SlaveMode              = SlaveMode;
        _config.SlaveName              = SlaveName;
        _config.CrashPredictionDiscord = CrashPredictionDiscord;
        _config.Save(); // single save — all settings written atomically

        System.IO.Directory.CreateDirectory(BackupPath);

        // Apply bot settings immediately
        _bot.ApplySettings(s);
        BotStatus = _bot.IsRunning ? "🟢 Running" : "⚫ Stopped";

        if (WebApiEnabled || SlaveMode)
        {
            _webApi.Start(WebApiPort, WebApiToken);
            WebApiStatus = BuildWebApiStatus();
        }
        else
        {
            _webApi.Stop();
            WebApiStatus = "⚫ Stopped";
        }

        SetStartWithWindows(StartWithWindows);

        WpfMsgBox.Show("Settings saved.", "WGS", WpfMsgBoxButton.OK, WpfMsgBoxImage.Information);
    }

    [RelayCommand]
    private void BrowseBackupPath()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description            = "Select backup folder",
            SelectedPath           = BackupPath,
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            BackupPath = dlg.SelectedPath;
    }

    [RelayCommand]
    private async Task TestDiscordAsync()
    {
        var ok = await _notifications.TestDiscordAsync();
        WpfMsgBox.Show(ok ? "Discord webhook test succeeded!" : "Test failed. Check the webhook URL.",
            "WGS Discord", WpfMsgBoxButton.OK,
            ok ? WpfMsgBoxImage.Information : WpfMsgBoxImage.Warning);
    }

    partial void OnWebApiPortChanged(int value) => OnPropertyChanged(nameof(SlaveConnectionUrl));
    partial void OnSlaveModeChanged(bool value) => OnPropertyChanged(nameof(SlaveConnectionUrl));

    private string BuildSlaveUrl()
    {
        // Try to get a useful local IP rather than 0.0.0.0
        string ip;
        try
        {
            using var s = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            s.Connect("8.8.8.8", 80);
            ip = (s.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "YOUR-IP";
        }
        catch { ip = "YOUR-IP"; }
        return $"http://{ip}:{WebApiPort}";
    }

    [RelayCommand]
    private void CopySlaveInfo()
    {
        var text = $"URL:   {SlaveConnectionUrl}\nToken: {WebApiToken}";
        try { System.Windows.Clipboard.SetText(text); }
        catch { }
    }

    private string BuildWebApiStatus()
    {
        if (!_webApi.IsRunning) return "⚫ Stopped";
        if (!_webApi.BoundToAllInterfaces)
        {
            OnPropertyChanged(nameof(WebApiStatusIsWarning));
            return $"⚠ localhost:{_webApi.Port} only — run WGS as Administrator or: " +
                   $"netsh http add urlacl url=http://+:{_webApi.Port}/ user=Everyone";
        }
        OnPropertyChanged(nameof(WebApiStatusIsWarning));
        return $"🟢 Running on port {_webApi.Port}  (network reachable)";
    }

    [RelayCommand]
    private async Task TestBotAsync()
    {
        if (string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(BotChannelId))
        {
            WpfMsgBox.Show("Enter Bot Token and Channel ID first.", "WGS", WpfMsgBoxButton.OK, WpfMsgBoxImage.Warning);
            return;
        }
        _bot.BotToken   = BotToken;
        _bot.ChannelId  = BotChannelId;
        var ok = await _bot.TestConnectionAsync();
        WpfMsgBox.Show(ok ? "Bot test message sent! Check your Discord channel." : "Test failed. Check Token and Channel ID.",
            "Discord Bot", WpfMsgBoxButton.OK,
            ok ? WpfMsgBoxImage.Information : WpfMsgBoxImage.Warning);
    }
}
