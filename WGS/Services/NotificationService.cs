using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using Newtonsoft.Json;
using WGS.Models;

namespace WGS.Services;

public class NotificationSettings
{
    // Webhook notifications
    public bool   DiscordEnabled    { get; set; }
    public string DiscordWebhookUrl { get; set; } = string.Empty;
    public bool   NotifyOnStart     { get; set; } = true;
    public bool   NotifyOnStop      { get; set; } = true;
    public bool   NotifyOnCrash     { get; set; } = true;
    public bool   NotifyOnUpdate    { get; set; } = true;

    // Remote control bot
    public bool   BotEnabled      { get; set; }
    public string BotToken        { get; set; } = string.Empty;
    public string BotChannelId    { get; set; } = string.Empty;
    public string BotPrefix       { get; set; } = "!";
    public string BotAllowedUsers { get; set; } = string.Empty;

    // Email (SMTP)
    public bool   EmailEnabled    { get; set; }
    public string SmtpHost        { get; set; } = string.Empty;
    public int    SmtpPort        { get; set; } = 587;
    public bool   SmtpSsl         { get; set; } = true;
    public string SmtpUser        { get; set; } = string.Empty;
    public string SmtpPassword    { get; set; } = string.Empty;
    public string EmailFrom       { get; set; } = string.Empty;
    public string EmailTo         { get; set; } = string.Empty; // comma-separated
}

// On-disk representation — secrets stored encrypted
file record NotificationSettingsData(
    bool   DiscordEnabled,
    string DiscordWebhookUrlEncrypted,
    bool   NotifyOnStart,
    bool   NotifyOnStop,
    bool   NotifyOnCrash,
    bool   NotifyOnUpdate,
    bool   BotEnabled                = false,
    string BotTokenEncrypted         = "",
    string BotChannelId              = "",
    string BotPrefix                 = "!",
    string BotAllowedUsers           = "",
    bool   EmailEnabled              = false,
    string SmtpHost                  = "",
    int    SmtpPort                  = 587,
    bool   SmtpSsl                   = true,
    string SmtpUser                  = "",
    string SmtpPasswordEncrypted     = "",
    string EmailFrom                 = "",
    string EmailTo                   = "");

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

        var encryptedToken = string.IsNullOrEmpty(_settings.BotToken)
            ? string.Empty
            : EncryptionService.Encrypt(_settings.BotToken);

        var encryptedSmtp = string.IsNullOrEmpty(_settings.SmtpPassword)
            ? string.Empty
            : EncryptionService.Encrypt(_settings.SmtpPassword);

        var data = new NotificationSettingsData(
            _settings.DiscordEnabled,
            encryptedUrl,
            _settings.NotifyOnStart,
            _settings.NotifyOnStop,
            _settings.NotifyOnCrash,
            _settings.NotifyOnUpdate,
            _settings.BotEnabled,
            encryptedToken,
            _settings.BotChannelId,
            _settings.BotPrefix,
            _settings.BotAllowedUsers,
            _settings.EmailEnabled,
            _settings.SmtpHost,
            _settings.SmtpPort,
            _settings.SmtpSsl,
            _settings.SmtpUser,
            encryptedSmtp,
            _settings.EmailFrom,
            _settings.EmailTo);

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
                    BotEnabled      = data.BotEnabled,
                    BotToken        = string.IsNullOrEmpty(data.BotTokenEncrypted)
                        ? string.Empty
                        : EncryptionService.Decrypt(data.BotTokenEncrypted),
                    BotChannelId    = data.BotChannelId,
                    BotPrefix       = string.IsNullOrEmpty(data.BotPrefix) ? "!" : data.BotPrefix,
                    BotAllowedUsers = data.BotAllowedUsers,
                    EmailEnabled    = data.EmailEnabled,
                    SmtpHost        = data.SmtpHost,
                    SmtpPort        = data.SmtpPort,
                    SmtpSsl         = data.SmtpSsl,
                    SmtpUser        = data.SmtpUser,
                    SmtpPassword    = string.IsNullOrEmpty(data.SmtpPasswordEncrypted)
                        ? string.Empty
                        : EncryptionService.Decrypt(data.SmtpPasswordEncrypted),
                    EmailFrom       = data.EmailFrom,
                    EmailTo         = data.EmailTo,
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
        var tasks = new List<Task>();
        if (_settings.DiscordEnabled && !string.IsNullOrWhiteSpace(_settings.DiscordWebhookUrl))
            tasks.Add(SendDiscordAsync(title, message, color));
        if (_settings.EmailEnabled && !string.IsNullOrWhiteSpace(_settings.EmailTo))
            tasks.Add(SendEmailAsync(title, message));
        if (tasks.Count > 0) await Task.WhenAll(tasks);
    }

    public async Task NotifyServerStatusAsync(GameServer server, ServerStatus status)
    {
        if (!server.DiscordAlertsEnabled) return;

        var (title, color) = status switch
        {
            ServerStatus.Running  when _settings.NotifyOnStart  => ($"✅ {server.DisplayName} started", "#3FB950"),
            ServerStatus.Stopped  when _settings.NotifyOnStop   => ($"⛔ {server.DisplayName} stopped", "#8B949E"),
            ServerStatus.Error    when _settings.NotifyOnCrash  => ($"💥 {server.DisplayName} crashed!", "#F85149"),
            ServerStatus.Updating when _settings.NotifyOnUpdate => ($"🔄 {server.DisplayName} updating", "#58A6FF"),
            _ => (null, null)
        };

        if (title != null) await NotifyAsync(title, $"Game: {GameRegistry_GameName(server.GameId)}", color!);
    }

    private static string GameRegistry_GameName(string gameId)
        => Games.GameRegistry.Get(gameId)?.GameName ?? gameId;

    private async Task SendEmailAsync(string subject, string body)
    {
        try
        {
            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl   = _settings.SmtpSsl,
                Credentials = new NetworkCredential(_settings.SmtpUser, _settings.SmtpPassword),
            };

            var html = $"""
                <html><body style="font-family:sans-serif;background:#0d1117;color:#c9d1d9;padding:24px">
                <h2 style="color:#58a6ff">{System.Net.WebUtility.HtmlEncode(subject)}</h2>
                <p>{System.Net.WebUtility.HtmlEncode(body)}</p>
                <hr style="border-color:#30363d"/><p style="color:#8b949e;font-size:12px">Windows Game Server</p>
                </body></html>
                """;

            var from = string.IsNullOrWhiteSpace(_settings.EmailFrom) ? _settings.SmtpUser : _settings.EmailFrom;
            var mail = new MailMessage { From = new MailAddress(from), Subject = subject, Body = html, IsBodyHtml = true };

            foreach (var addr in _settings.EmailTo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                mail.To.Add(addr);

            await client.SendMailAsync(mail);
        }
        catch { }
    }

    public async Task<bool> TestEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost) || string.IsNullOrWhiteSpace(_settings.EmailTo))
            return false;
        try
        {
            await SendEmailAsync("🧪 WGS Test Email", "Windows Game Server email notifications are working!");
            return true;
        }
        catch { return false; }
    }

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
