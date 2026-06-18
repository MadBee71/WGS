using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WGS.Services;

namespace WGS.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly NotificationService _notifications;
    private readonly ConfigService       _config;
    private readonly SteamCmdService     _steamCmd;
    private readonly DiscordBotService   _bot;
    private readonly WebApiService       _webApi;
    private readonly UPnPService         _upnp;

    // ── Webhook notifications ─────────────────────────────────────────────────
    [ObservableProperty] private bool   _discordEnabled;
    [ObservableProperty] private string _discordWebhookUrl = string.Empty;
    [ObservableProperty] private bool   _notifyOnStart     = true;
    [ObservableProperty] private bool   _notifyOnStop      = true;
    [ObservableProperty] private bool   _notifyOnCrash     = true;
    [ObservableProperty] private bool   _notifyOnUpdate    = true;
    [ObservableProperty] private bool   _notifyOnPlayerJoin  = false;
    [ObservableProperty] private bool   _notifyOnPlayerLeave = false;

    // ── Discord remote-control bot ────────────────────────────────────────────
    [ObservableProperty] private bool   _botEnabled;
    [ObservableProperty] private string _botToken        = string.Empty;
    [ObservableProperty] private string _botChannelId    = string.Empty;
    [ObservableProperty] private string _botPrefix       = "!";
    [ObservableProperty] private string _botAllowedUsers = string.Empty;
    [ObservableProperty] private string _botStatus       = string.Empty;
    [ObservableProperty] private string _newBotAdminId   = string.Empty;
    [ObservableProperty] private string? _selectedBotAdmin;
    public ObservableCollection<string> BotAdminList { get; } = [];
    public bool BotIsRunning => BotEnabled && _bot.IsRunning;

    // ── Email (SMTP) ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _emailEnabled;
    [ObservableProperty] private string _smtpHost     = string.Empty;
    [ObservableProperty] private int    _smtpPort     = 587;
    [ObservableProperty] private bool   _smtpSsl      = true;
    [ObservableProperty] private string _smtpUser     = string.Empty;
    [ObservableProperty] private string _smtpPassword = string.Empty;
    [ObservableProperty] private string _emailFrom    = string.Empty;
    [ObservableProperty] private string _emailTo      = string.Empty;

    // ── Web API ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _webApiEnabled;
    [ObservableProperty] private int    _webApiPort    = 8765;
    [ObservableProperty] private string _webApiToken   = string.Empty;
    [ObservableProperty] private string _webApiStatus  = string.Empty;
    public bool WebApiStatusIsWarning => WebApiStatus.StartsWith("⚠");
    public bool WebApiIsRunning       => WebApiEnabled && _webApi.IsRunning;

    // ── Slave / Remote Control ────────────────────────────────────────────────
    [ObservableProperty] private bool   _slaveMode;
    [ObservableProperty] private string _slaveName = "This Machine";

    public string SlaveConnectionUrl => BuildSlaveUrl();

    // ── Crash Prediction ──────────────────────────────────────────────────────
    [ObservableProperty] private bool   _crashPredictionDiscord;
    [ObservableProperty] private bool   _crashPredictionLowMemOnly;
    [ObservableProperty] private double _crashPredictionLowMemPercent = 5.0;
    [ObservableProperty] private bool   _crashPredictionHighCpuOnly;
    [ObservableProperty] private double _crashPredictionHighCpuPercent = 98.0;

    // ── UPnP ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _enableUPnP;
    [ObservableProperty] private string _upnpStatus = string.Empty;
    public bool UpnpIsFound => UpnpStatus.StartsWith("Router found");

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
                             SteamCmdService steamCmd, DiscordBotService bot, WebApiService webApi,
                             UPnPService upnp)
    {
        _notifications = notifications;
        _config        = config;
        _steamCmd      = steamCmd;
        _bot           = bot;
        _webApi        = webApi;
        _upnp          = upnp;
        _bot.StatusChanged += msg => WpfApplication.Current?.Dispatcher?.Invoke(() => {
            BotStatus = msg;
            OnPropertyChanged(nameof(BotIsRunning));
        });
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
        NotifyOnPlayerJoin  = s.NotifyOnPlayerJoin;
        NotifyOnPlayerLeave = s.NotifyOnPlayerLeave;
        BotEnabled         = s.BotEnabled;
        BotToken           = s.BotToken        ?? string.Empty;
        BotChannelId       = s.BotChannelId    ?? string.Empty;
        BotPrefix          = string.IsNullOrEmpty(s.BotPrefix) ? "!" : s.BotPrefix;
        BotAllowedUsers    = s.BotAllowedUsers ?? string.Empty;
        BotAdminList.Clear();
        foreach (var id in (s.BotAllowedUsers ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            BotAdminList.Add(id);
        EmailEnabled       = s.EmailEnabled;
        SmtpHost           = s.SmtpHost     ?? string.Empty;
        SmtpPort           = s.SmtpPort == 0 ? 587 : s.SmtpPort;
        SmtpSsl            = s.SmtpSsl;
        SmtpUser           = s.SmtpUser     ?? string.Empty;
        SmtpPassword       = s.SmtpPassword ?? string.Empty;
        EmailFrom          = s.EmailFrom    ?? string.Empty;
        EmailTo            = s.EmailTo      ?? string.Empty;
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
        CrashPredictionLowMemOnly = _config.CrashPredictionLowMemOnly;
        CrashPredictionLowMemPercent = _config.CrashPredictionLowMemPercent;
        CrashPredictionHighCpuOnly = _config.CrashPredictionHighCpuOnly;
        CrashPredictionHighCpuPercent = _config.CrashPredictionHighCpuPercent;
        EnableUPnP               = _config.EnableUPnP;
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
        s.NotifyOnPlayerJoin  = NotifyOnPlayerJoin;
        s.NotifyOnPlayerLeave = NotifyOnPlayerLeave;
        s.BotEnabled        = BotEnabled;
        s.BotToken          = BotToken;
        s.BotChannelId      = BotChannelId;
        s.BotPrefix         = BotPrefix;
        s.BotAllowedUsers   = string.Join(",", BotAdminList);
        s.EmailEnabled      = EmailEnabled;
        s.SmtpHost          = SmtpHost;
        s.SmtpPort          = SmtpPort;
        s.SmtpSsl           = SmtpSsl;
        s.SmtpUser          = SmtpUser;
        s.SmtpPassword      = SmtpPassword;
        s.EmailFrom         = EmailFrom;
        s.EmailTo           = EmailTo;
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
        _config.CrashPredictionLowMemOnly = CrashPredictionLowMemOnly;
        _config.CrashPredictionLowMemPercent = CrashPredictionLowMemPercent > 0 ? CrashPredictionLowMemPercent : 5.0;
        _config.CrashPredictionHighCpuOnly = CrashPredictionHighCpuOnly;
        _config.CrashPredictionHighCpuPercent = CrashPredictionHighCpuPercent > 0 ? CrashPredictionHighCpuPercent : 98.0;
        _config.EnableUPnP             = EnableUPnP;
        if (!EnableUPnP) { UpnpStatus = "Stopped"; OnPropertyChanged(nameof(UpnpIsFound)); }
        _config.Save(); // single save — all settings written atomically

        System.IO.Directory.CreateDirectory(BackupPath);

        // Apply bot settings immediately
        _bot.ApplySettings(s);
        BotStatus = _bot.IsRunning ? "Running" : "Stopped";
        OnPropertyChanged(nameof(BotIsRunning));

        _webApi.DashboardEnabled = WebApiEnabled;
        if (WebApiEnabled || SlaveMode)
        {
            _webApi.Start(WebApiPort, WebApiToken);
            WebApiStatus = BuildWebApiStatus();
        }
        else
        {
            _webApi.Stop();
            WebApiStatus = "Stopped";
            OnPropertyChanged(nameof(WebApiIsRunning));
            OnPropertyChanged(nameof(WebApiStatusIsWarning));
        }

        SetStartWithWindows(StartWithWindows);

        WpfMsgBox.Show("Settings saved.", "WGS", WpfMsgBoxButton.OK, WpfMsgBoxImage.Information);
    }

    [RelayCommand]
    private void AddBotAdmin()
    {
        var id = NewBotAdminId.Trim();
        if (string.IsNullOrEmpty(id) || BotAdminList.Contains(id)) return;
        BotAdminList.Add(id);
        NewBotAdminId = string.Empty;
    }

    [RelayCommand]
    private void RemoveBotAdmin()
    {
        if (SelectedBotAdmin != null)
            BotAdminList.Remove(SelectedBotAdmin);
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

    [RelayCommand]
    private async Task TestEmailAsync()
    {
        SaveSettings();
        var ok = await _notifications.TestEmailAsync();
        WpfMsgBox.Show(ok ? "Test email sent successfully!" : "Failed to send. Check SMTP settings.",
            "WGS Email", WpfMsgBoxButton.OK,
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
        OnPropertyChanged(nameof(WebApiIsRunning));
        OnPropertyChanged(nameof(WebApiStatusIsWarning));
        if (!_webApi.IsRunning) return "Stopped";
        if (!_webApi.BoundToAllInterfaces)
            return $"localhost:{_webApi.Port} only — run WGS as Administrator or: " +
                   $"netsh http add urlacl url=http://+:{_webApi.Port}/ user=Everyone";
        return $"Running on port {_webApi.Port}  (network reachable)";
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

    [RelayCommand]
    private async Task TestUPnPAsync()
    {
        UpnpStatus = "Discovering router...";
        OnPropertyChanged(nameof(UpnpIsFound));
        var ok = await _upnp.DiscoverAsync();
        UpnpStatus = ok ? "Router found — UPnP is available" : "Router not found or UPnP is disabled";
        OnPropertyChanged(nameof(UpnpIsFound));
    }
}
