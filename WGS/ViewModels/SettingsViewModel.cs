using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WGS.Services;
using WpfMsgBox = System.Windows.MessageBox;
using WpfMsgBoxButton = System.Windows.MessageBoxButton;
using WpfMsgBoxImage = System.Windows.MessageBoxImage;

namespace WGS.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly NotificationService _notifications;
    private readonly ConfigService _config;
    private readonly SteamCmdService _steamCmd;

    [ObservableProperty] private bool   _discordEnabled;
    [ObservableProperty] private string _discordWebhookUrl = string.Empty;
    [ObservableProperty] private bool   _notifyOnStart     = true;
    [ObservableProperty] private bool   _notifyOnStop      = true;
    [ObservableProperty] private bool   _notifyOnCrash     = true;
    [ObservableProperty] private bool   _notifyOnUpdate    = true;
    [ObservableProperty] private string _defaultInstallRoot = string.Empty;
    [ObservableProperty] private string _steamCmdPath       = string.Empty;
    [ObservableProperty] private string _steamLogin         = string.Empty;
    [ObservableProperty] private string _steamPassword      = string.Empty;

    public SettingsViewModel(NotificationService notifications, ConfigService config, SteamCmdService steamCmd)
    {
        _notifications = notifications;
        _config        = config;
        _steamCmd      = steamCmd;
        Load();
    }

    private void Load()
    {
        var s = _notifications.Settings;
        DiscordEnabled    = s.DiscordEnabled;
        DiscordWebhookUrl = s.DiscordWebhookUrl;
        NotifyOnStart     = s.NotifyOnStart;
        NotifyOnStop      = s.NotifyOnStop;
        NotifyOnCrash     = s.NotifyOnCrash;
        NotifyOnUpdate    = s.NotifyOnUpdate;
        DefaultInstallRoot = _config.DefaultInstallRoot;
        SteamCmdPath  = System.IO.Path.Combine(_config.AppDataPath, "steamcmd");
        SteamLogin    = _config.SteamLogin;
        SteamPassword = _config.SteamPassword;
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
        _notifications.Save();
        _config.DefaultInstallRoot = DefaultInstallRoot;
        _config.SteamLogin    = SteamLogin;
        _config.SteamPassword = SteamPassword;
        _config.Save();
        WpfMsgBox.Show("Settings saved.", "WGS", WpfMsgBoxButton.OK, WpfMsgBoxImage.Information);
    }

    [RelayCommand]
    private async Task TestDiscordAsync()
    {
        var ok = await _notifications.TestDiscordAsync();
        WpfMsgBox.Show(ok ? "Discord test succeeded!" : "Test failed. Check the webhook URL.",
            "WGS Discord", WpfMsgBoxButton.OK,
            ok ? WpfMsgBoxImage.Information : WpfMsgBoxImage.Warning);
    }
}
