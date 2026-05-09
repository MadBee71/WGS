using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Lightweight Discord remote-control bot using the Discord HTTP API with
/// long-poll message scanning — no external library required.
///
/// Setup:
///   1. Create a Bot at https://discord.com/developers/applications
///   2. Give it Read/Send Messages permissions in your control channel
///   3. Copy the Bot Token and the Channel ID into WGS Settings → Discord Bot
///
/// Commands (prefix configurable, default !):
///   !help                    — list commands
///   !status                  — list all servers + state
///   !start   &lt;name&gt;         — start a server
///   !stop    &lt;name&gt;         — stop a server
///   !restart &lt;name&gt;         — restart a server
///   !update  &lt;name&gt;         — update a server
///   !backup  &lt;name&gt;         — create a backup
///   !cmd     &lt;name&gt; &lt;cmd&gt;   — send console command to server
/// </summary>
public class DiscordBotService : IDisposable
{
    // ── Dependencies ─────────────────────────────────────────────────────────
    private readonly ServerManagerService _manager;
    private readonly SteamCmdService      _steam;
    private readonly BackupService        _backup;

    // ── Settings ─────────────────────────────────────────────────────────────
    public string  BotToken       { get; set; } = string.Empty;
    public string  ChannelId      { get; set; } = string.Empty;
    public string  CommandPrefix  { get; set; } = "!";
    /// <summary>Comma-separated Discord user IDs allowed to send commands. Empty = anyone in the channel.</summary>
    public string  AllowedUserIds { get; set; } = string.Empty;
    public bool    IsEnabled      { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly HttpClient     _http   = new();
    private CancellationTokenSource _cts    = new();
    private Task?                   _loop;
    private string                  _lastMessageId = "0";

    public bool IsRunning => _loop is { IsCompleted: false };
    public event Action<string>? StatusChanged;

    // Servers are set externally by MainViewModel so the bot can look them up
    public Func<IEnumerable<GameServer>>? GetServers { get; set; }
    public Func<string, Task>?            StartServer { get; set; }
    public Func<string, Task>?            StopServer  { get; set; }
    public Func<string, Task>?            RestartServer { get; set; }
    public Func<string, Task>?            UpdateServer  { get; set; }
    public Func<string, Task>?            BackupServer  { get; set; }
    public Func<string, string, Task>?    SendCmd       { get; set; }

    public DiscordBotService(ServerManagerService manager, SteamCmdService steam, BackupService backup)
    {
        _manager = manager;
        _steam   = steam;
        _backup  = backup;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Start()
    {
        if (string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(ChannelId)) return;
        Stop();
        _cts  = new CancellationTokenSource();
        _loop = Task.Run(() => PollLoop(_cts.Token));
        StatusChanged?.Invoke("✅ Discord bot started");
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _loop?.Wait(2000); } catch { }
        _loop = null;
        StatusChanged?.Invoke("⛔ Discord bot stopped");
    }

    public void ApplySettings(NotificationSettings settings)
    {
        var wasRunning = IsRunning;
        if (wasRunning) Stop();

        IsEnabled      = settings.BotEnabled;
        BotToken       = settings.BotToken;
        ChannelId      = settings.BotChannelId;
        CommandPrefix  = string.IsNullOrWhiteSpace(settings.BotPrefix) ? "!" : settings.BotPrefix;
        AllowedUserIds = settings.BotAllowedUsers;

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bot", BotToken);

        if (IsEnabled && !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChannelId))
            Start();
    }

    // ── Poll loop ─────────────────────────────────────────────────────────────

    private async Task PollLoop(CancellationToken ct)
    {
        // Seed _lastMessageId so we don't replay old messages on startup
        await SeedLastMessageId(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollMessages(ct);
                await Task.Delay(3000, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"⚠ Bot error: {ex.Message}");
                await Task.Delay(15000, ct); // back off on error
            }
        }
    }

    private async Task SeedLastMessageId(CancellationToken ct)
    {
        try
        {
            var url  = $"https://discord.com/api/v10/channels/{ChannelId}/messages?limit=1";
            var json = await _http.GetStringAsync(url, ct);
            var arr  = JArray.Parse(json);
            if (arr.Count > 0)
                _lastMessageId = arr[0]["id"]!.ToString();
        }
        catch { }
    }

    private async Task PollMessages(CancellationToken ct)
    {
        var url  = $"https://discord.com/api/v10/channels/{ChannelId}/messages?after={_lastMessageId}&limit=10";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return;

        var json = await resp.Content.ReadAsStringAsync(ct);
        var msgs = JArray.Parse(json);
        if (msgs.Count == 0) return;

        // Messages come newest-first, reverse to process oldest first
        var ordered = msgs.Reverse().ToList();
        foreach (var msg in ordered)
        {
            var id       = msg["id"]!.ToString();
            var authorId = msg["author"]?["id"]?.ToString() ?? "";
            var isBot    = msg["author"]?["bot"]?.Value<bool>() ?? false;
            var content  = msg["content"]?.ToString() ?? "";

            _lastMessageId = id; // always advance cursor

            if (isBot) continue;
            if (!content.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            // Authorization check
            if (!string.IsNullOrWhiteSpace(AllowedUserIds))
            {
                var allowed = AllowedUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!allowed.Contains(authorId)) continue;
            }

            await HandleCommand(id, content, ct);
        }
    }

    // ── Command router ────────────────────────────────────────────────────────

    private async Task HandleCommand(string triggerMsgId, string content, CancellationToken ct)
    {
        var parts   = content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0][CommandPrefix.Length..].ToLowerInvariant();
        var args    = parts.Skip(1).ToArray();

        try
        {
            var reply = command switch
            {
                "help"    => BuildHelp(),
                "status"  => BuildStatus(),
                "list"    => BuildStatus(),
                "start"   => await RunServerAction(args, StartServer,   "▶️ Starting",   "start"),
                "stop"    => await RunServerAction(args, StopServer,    "⏹ Stopping",    "stop"),
                "restart" => await RunServerAction(args, RestartServer, "🔄 Restarting", "restart"),
                "update"  => await RunServerAction(args, UpdateServer,  "🔄 Updating",   "update"),
                "backup"  => await RunServerAction(args, BackupServer,  "💾 Backing up", "backup"),
                "cmd"     => await RunConsoleCmd(args),
                _         => $"❓ Unknown command `{command}`. Type `{CommandPrefix}help`."
            };

            await SendMessage(reply, ct);
        }
        catch (Exception ex)
        {
            await SendMessage($"💥 Error: {ex.Message}", ct);
        }
    }

    private string BuildHelp() =>
        $"**Windows Game Server — Remote Commands**\n" +
        $"`{CommandPrefix}status`                  — list all servers\n" +
        $"`{CommandPrefix}start   <name>`          — start server\n" +
        $"`{CommandPrefix}stop    <name>`          — stop server\n" +
        $"`{CommandPrefix}restart <name>`          — restart server\n" +
        $"`{CommandPrefix}update  <name>`          — update server\n" +
        $"`{CommandPrefix}backup  <name>`          — create backup\n" +
        $"`{CommandPrefix}cmd     <name> <cmd>`    — send console command\n" +
        $"*Name matching is case-insensitive and partial.*";

    private string BuildStatus()
    {
        var servers = GetServers?.Invoke().ToList() ?? [];
        if (servers.Count == 0) return "No servers configured.";

        var sb = new StringBuilder("**Server Status**\n");
        foreach (var s in servers)
        {
            var icon = s.Status switch
            {
                ServerStatus.Running    => "🟢",
                ServerStatus.Stopped    => "🔴",
                ServerStatus.Starting   => "🟡",
                ServerStatus.Stopping   => "🟡",
                ServerStatus.Installing => "🔵",
                ServerStatus.Updating   => "🔵",
                ServerStatus.Error      => "💥",
                _                       => "⚪",
            };
            sb.AppendLine($"{icon} **{s.DisplayName}** — {s.Status}");
        }
        return sb.ToString().TrimEnd();
    }

    private async Task<string> RunServerAction(string[] args,
        Func<string, Task>? action, string verb, string cmdName)
    {
        if (args.Length == 0)
            return $"Usage: `{CommandPrefix}{cmdName} <server name>`";
        if (action == null)
            return "⚠ Action not wired up.";

        var name = string.Join(" ", args);
        var srv  = FindServer(name);
        if (srv == null)
            return $"❌ Server not found: `{name}`\nUse `{CommandPrefix}status` to see server names.";

        await action(srv.Id);
        return $"{verb} **{srv.DisplayName}**...";
    }

    private async Task<string> RunConsoleCmd(string[] args)
    {
        if (args.Length < 2)
            return $"Usage: `{CommandPrefix}cmd <server name> <command>`";

        var name = args[0];
        var cmd  = string.Join(" ", args.Skip(1));
        var srv  = FindServer(name);
        if (srv == null)
            return $"❌ Server not found: `{name}`";

        if (SendCmd != null)
            await SendCmd(srv.Id, cmd);
        else
            _manager.SendCommand(srv.Id, cmd);

        return $"📨 Sent to **{srv.DisplayName}**: `{cmd}`";
    }

    private GameServer? FindServer(string namePart)
    {
        var servers = GetServers?.Invoke().ToList() ?? [];
        return servers.FirstOrDefault(s =>
            s.DisplayName.Equals(namePart, StringComparison.OrdinalIgnoreCase))
            ?? servers.FirstOrDefault(s =>
            s.DisplayName.Contains(namePart, StringComparison.OrdinalIgnoreCase));
    }

    // ── Discord HTTP ──────────────────────────────────────────────────────────

    private async Task SendMessage(string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ChannelId)) return;
        try
        {
            // Discord has a 2000-char limit
            if (content.Length > 1990) content = content[..1990] + "…";
            var payload = JsonConvert.SerializeObject(new { content });
            var req     = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync(
                $"https://discord.com/api/v10/channels/{ChannelId}/messages",
                req, ct);
        }
        catch { }
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(ChannelId))
            return false;
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bot", BotToken);
            await SendMessage("🧪 WGS Discord bot connection test — OK!");
            return true;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
        _cts.Dispose();
    }
}
