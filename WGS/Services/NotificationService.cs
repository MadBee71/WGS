using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using WGS.Models;

namespace WGS.Services;

public class NotificationSettings
{
    public bool DiscordEnabled { get; set; }
    public string DiscordWebhookUrl { get; set; } = string.Empty;
    public bool NotifyOnStart  { get; set; } = true;
    public bool NotifyOnStop   { get; set; } = true;
    public bool NotifyOnCrash  { get; set; } = true;
    public bool NotifyOnUpdate { get; set; } = true;
}

// On-disk representation — webhook is stored encrypted
file record NotificationSettingsData(
    bool DiscordEnabled,
    string DiscordWebhookUrlEncrypted,
    bool NotifyOnStart,
    bool NotifyOnStop,
    bool NotifyOnCrash,
    bool NotifyOnUpdate);

public class NotificationService
{
    private readonly ConfigService _config;
    private NotificationSettings _settings = new();
    private readonly string _settingsFile;
    private static readonly HttpClient _http = new();

    public NotificationSettings Settings => _settings;

    public NotificationService(ConfigService config)
    {
        _config       = config;
        _settingsFile = System.IO.Path.Combine(config.AppDataPath, "notifications.json");
        Load();
    }

    public void Save()
    {
        var encryptedUrl = string.IsNullOrEmpty(_settings.DiscordWebhookUrl)
            ? string.Empty
            : EncryptionService.Encrypt(_settings.DiscordWebhookUrl);

        var data = new NotificationSettingsData(
            _settings.DiscordEnabled,
            encryptedUrl,
            _settings.NotifyOnStart,
            _settings.NotifyOnStop,
            _settings.NotifyOnCrash,
            _settings.NotifyOnUpdate);

        System.IO.File.WriteAllText(_settingsFile, JsonConvert.SerializeObject(data, Formatting.Indented));
    }

    public void Load()
    {
        if (!System.IO.File.Exists(_settingsFile)) return;
        try
        {
            var raw = System.IO.File.ReadAllText(_settingsFile);

            // Try new encrypted format first
            var data = JsonConvert.DeserializeObject<NotificationSettingsData>(raw);
            if (data != null)
            {
                _settings = new NotificationSettings
                {
                    DiscordEnabled    = data.DiscordEnabled,
                    DiscordWebhookUrl = string.IsNullOrEmpty(data.DiscordWebhookUrlEncrypted)
                        ? string.Empty
                        : EncryptionService.Decrypt(data.DiscordWebhookUrlEncrypted),
                    NotifyOnStart  = data.NotifyOnStart,
                    NotifyOnStop   = data.NotifyOnStop,
                    NotifyOnCrash  = data.NotifyOnCrash,
                    NotifyOnUpdate = data.NotifyOnUpdate,
                };
                return;
            }

            // Fallback: old plain-text format
            _settings = JsonConvert.DeserializeObject<NotificationSettings>(raw) ?? new();
        }
        catch { }
    }

    public async Task NotifyAsync(string title, string message, string color = "#58A6FF")
    {
        if (_settings.DiscordEnabled && !string.IsNullOrWhiteSpace(_settings.DiscordWebhookUrl))
            await SendDiscordAsync(title, message, color);
    }

    public async Task NotifyServerStatusAsync(GameServer server, ServerStatus status)
    {
        var (title, color) = status switch
        {
            ServerStatus.Running  when _settings.NotifyOnStart  => ($"✅ {server.DisplayName} started", "#3FB950"),
            ServerStatus.Stopped  when _settings.NotifyOnStop   => ($"⛔ {server.DisplayName} stopped", "#F85149"),
            ServerStatus.Error    when _settings.NotifyOnCrash  => ($"💥 {server.DisplayName} crashed!", "#F85149"),
            ServerStatus.Updating when _settings.NotifyOnUpdate => ($"🔄 {server.DisplayName} updating", "#58A6FF"),
            _ => (null, null)
        };

        if (title != null) await NotifyAsync(title, $"Game: {GameRegistry_GameName(server.GameId)}", color!);
    }

    private static string GameRegistry_GameName(string gameId)
        => Games.GameRegistry.Get(gameId)?.GameName ?? gameId;

    private async Task SendDiscordAsync(string title, string description, string hexColor)
    {
        if (!Uri.TryCreate(_settings.DiscordWebhookUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            int colorInt = Convert.ToInt32(hexColor.TrimStart('#'), 16);
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color = colorInt,
                        timestamp = DateTime.UtcNow.ToString("o"),
                        footer = new { text = "Windows Game Server" }
                    }
                }
            };
            var json    = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _http.PostAsync(uri, content);
        }
        catch { }
    }

    public async Task<bool> TestDiscordAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.DiscordWebhookUrl)) return false;
        try
        {
            await SendDiscordAsync("🧪 Test", "WGS Discord notifications are working!", "#58A6FF");
            return true;
        }
        catch { return false; }
    }
}
