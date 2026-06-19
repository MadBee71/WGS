using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
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
    // ── Settings ─────────────────────────────────────────────────────────────
    public string  BotToken       { get; set; } = string.Empty;
    public string  ChannelId      { get; set; } = string.Empty;
    public string  CommandPrefix  { get; set; } = "!";
    /// <summary>Comma-separated Discord user IDs allowed to send commands. Empty = anyone in the channel.</summary>
    public string  AllowedUserIds { get; set; } = string.Empty;
    public bool    IsEnabled      { get; set; }
    /// <summary>When true, a single message in StatusChannelId is edited in place with live status instead of posting new ones.</summary>
    public bool    StatusEnabled    { get; set; }
    /// <summary>Channel for the live status message. Falls back to ChannelId when empty.</summary>
    public string  StatusChannelId  { get; set; } = string.Empty;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly HttpClient     _http   = new();
    private CancellationTokenSource _cts    = new();
    private Task?                   _loop;
    private Task?                   _statusLoop;
    private string                  _lastMessageId = "0";
    /// <summary>ID of the live status message, kept in memory only — a fresh message is posted after a restart.</summary>
    private string?                 _statusMessageId;
    /// <summary>Channel the current _statusMessageId actually lives in, so a channel change can be detected and the old message cleaned up.</summary>
    private string?                 _statusMessageChannelId;

    // ── Gateway (for button interactions only — everything else uses REST polling) ──
    private Task?  _gatewayLoop;
    private long?  _gatewaySeq;
    private const string WakeButtonPrefix = "wgs_wake_";

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

    public DiscordBotService() { }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Start()
    {
        if (string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(ChannelId)) return;
        Stop();
        _cts  = new CancellationTokenSource();
        _loop = Task.Run(() => PollLoop(_cts.Token));
        if (StatusEnabled)
        {
            _statusMessageId        = null; // post a fresh message this run instead of guessing an old one is still valid
            _statusMessageChannelId = null;
            _statusLoop  = Task.Run(() => StatusUpdateLoop(_cts.Token));
            _gatewayLoop = Task.Run(() => GatewayLoop(_cts.Token));
        }
        StatusChanged?.Invoke("✅ Discord bot started");
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _loop?.Wait(2000); } catch { }
        try { _statusLoop?.Wait(2000); } catch { }
        try { _gatewayLoop?.Wait(2000); } catch { }
        _loop = null;
        _statusLoop = null;
        _gatewayLoop = null;
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
        StatusEnabled    = settings.BotStatusEnabled;
        StatusChannelId  = string.IsNullOrWhiteSpace(settings.BotStatusChannelId) ? ChannelId : settings.BotStatusChannelId;

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

    // ── Live status message ─────────────────────────────────────────────────
    // Keeps one message in StatusChannelId updated in place (edit, not re-post) — same
    // pattern community status bots use for device/server dashboards in a pinned message.

    private async Task StatusUpdateLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PostOrEditStatusMessage(ct);
                await Task.Delay(60_000, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(60_000, ct); }
        }
    }

    private async Task PostOrEditStatusMessage(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(StatusChannelId)) return;

        // If the channel was changed in Settings since the last post, the old message is left
        // behind in the old channel forever unless we explicitly delete it here first.
        if (_statusMessageId != null && _statusMessageChannelId != null && _statusMessageChannelId != StatusChannelId)
        {
            try
            {
                await _http.DeleteAsync(
                    $"https://discord.com/api/v10/channels/{_statusMessageChannelId}/messages/{_statusMessageId}", ct);
            }
            catch { }
            _statusMessageId = null;
        }

        var embed      = BuildStatusEmbed();
        var components = BuildWakeButtonRows();
        var payload     = JsonConvert.SerializeObject(new { embeds = new[] { embed }, components });

        if (_statusMessageId != null)
        {
            var editResp = await _http.PatchAsync(
                $"https://discord.com/api/v10/channels/{StatusChannelId}/messages/{_statusMessageId}",
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);

            if (editResp.IsSuccessStatusCode) return;
            if (editResp.StatusCode != System.Net.HttpStatusCode.NotFound) return; // transient error, retry next tick
            _statusMessageId = null; // message was deleted — fall through and post a new one
        }

        var postResp = await _http.PostAsync(
            $"https://discord.com/api/v10/channels/{StatusChannelId}/messages",
            new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        if (!postResp.IsSuccessStatusCode) return;

        var json = await postResp.Content.ReadAsStringAsync(ct);
        _statusMessageId        = JObject.Parse(json)["id"]?.ToString();
        _statusMessageChannelId = StatusChannelId;
    }

    private object BuildStatusEmbed()
    {
        var servers = GetServers?.Invoke().ToList() ?? [];
        var fields = servers.Select(s =>
        {
            var online = s.Status == ServerStatus.Running;
            var ip     = string.IsNullOrEmpty(s.ServerIp) || s.ServerIp == "0.0.0.0" ? "127.0.0.1" : s.ServerIp;
            var port   = s.QueryPort > 0 ? s.QueryPort : s.ServerPort;
            var line1  = online ? $"🟢 Online — {s.CurrentPlayers}/{s.MaxPlayers} players" : "⚫ Offline";
            var value  = $"{line1}\nConnect: `{ip}:{port}`";
            return new { name = s.DisplayName, value, inline = true };
        }).ToArray();

        return new
        {
            title       = "Server Status",
            color       = 0x1F6FEB,
            fields,
            footer      = new { text = "Windows Game Server · updates every minute" },
            timestamp   = DateTime.UtcNow.ToString("o"),
        };
    }

    /// <summary>One "Wake" button per offline, wake-on-demand-enabled server, grouped into rows of 5 (Discord's max per row).</summary>
    private object[] BuildWakeButtonRows()
    {
        var servers = GetServers?.Invoke()
            .Where(s => s.Status != ServerStatus.Running && s.WakeOnDemand)
            .ToList() ?? [];
        if (servers.Count == 0) return [];

        var buttons = servers.Select(s =>
        {
            var label = "Wake " + s.DisplayName;
            if (label.Length > 80) label = label[..80];
            return new { type = 2, style = 1, label, custom_id = WakeButtonPrefix + s.Id }; // type 2 = Button, style 1 = Primary
        }).ToList();

        var rows = new List<object>();
        for (int i = 0; i < buttons.Count && rows.Count < 5; i += 5)
            rows.Add(new { type = 1, components = buttons.Skip(i).Take(5).ToArray() }); // 1 = Action Row

        return rows.ToArray();
    }

    // ── Gateway connection — only needed to receive button-click interactions ──────
    // (everything else — commands, status edits — works over plain REST polling)

    private async Task GatewayLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RunGatewaySessionAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch { }
            if (ct.IsCancellationRequested) break;
            try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { break; } // reconnect backoff
        }
    }

    private async Task RunGatewaySessionAsync(CancellationToken ct)
    {
        string gatewayUrl;
        try
        {
            var json = await _http.GetStringAsync("https://discord.com/api/v10/gateway", ct);
            gatewayUrl = JObject.Parse(json)["url"]?.ToString() ?? "wss://gateway.discord.gg";
        }
        catch { gatewayUrl = "wss://gateway.discord.gg"; }

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"{gatewayUrl}/?v=10&encoding=json"), ct);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task? heartbeatTask = null;

        try
        {
            var buffer = new byte[16 * 1024];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var msg = JObject.Parse(Encoding.UTF8.GetString(ms.ToArray()));
                var op  = msg["op"]?.Value<int>() ?? -1;
                if (msg["s"] != null) _gatewaySeq = msg["s"]!.Value<long>();

                switch (op)
                {
                    case 10: // Hello — start heartbeating, then identify
                        var interval = msg["d"]?["heartbeat_interval"]?.Value<int>() ?? 41250;
                        heartbeatTask = Task.Run(() => HeartbeatLoop(ws, interval, heartbeatCts.Token), heartbeatCts.Token);
                        await SendIdentifyAsync(ws, ct);
                        break;
                    case 1: // server requests an immediate heartbeat
                        await SendHeartbeatAsync(ws, ct);
                        break;
                    case 7:  // Reconnect requested
                    case 9:  // Invalid session
                        return; // drop and let GatewayLoop reconnect fresh
                    case 0:  // Dispatch
                        var t = msg["t"]?.ToString();
                        if (t == "INTERACTION_CREATE")
                            _ = HandleInteractionAsync(msg["d"] as JObject, ct);
                        break;
                }
            }
        }
        finally
        {
            heartbeatCts.Cancel();
            try { if (heartbeatTask != null) await heartbeatTask; } catch { }
        }
    }

    private async Task HeartbeatLoop(ClientWebSocket ws, int intervalMs, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                await Task.Delay(intervalMs, ct);
                await SendHeartbeatAsync(ws, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SendHeartbeatAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var payload = JsonConvert.SerializeObject(new { op = 1, d = _gatewaySeq });
        await SendWsAsync(ws, payload, ct);
    }

    private async Task SendIdentifyAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var payload = JsonConvert.SerializeObject(new
        {
            op = 2,
            d  = new
            {
                token      = BotToken,
                intents    = 0, // interaction events aren't gated by intents
                properties = new { os = "windows", browser = "wgs", device = "wgs" },
            }
        });
        await SendWsAsync(ws, payload, ct);
    }

    private static async Task SendWsAsync(ClientWebSocket ws, string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private async Task HandleInteractionAsync(JObject? interaction, CancellationToken ct)
    {
        if (interaction == null) return;
        var customId = interaction["data"]?["custom_id"]?.ToString();
        if (string.IsNullOrEmpty(customId) || !customId.StartsWith(WakeButtonPrefix)) return;

        var serverId   = customId[WakeButtonPrefix.Length..];
        var server     = GetServers?.Invoke().FirstOrDefault(s => s.Id == serverId);
        var serverName = server?.DisplayName ?? "server";
        var id         = interaction["id"]?.ToString();
        var token      = interaction["token"]?.ToString();
        if (id == null || token == null) return;

        // Must ack within 3 seconds — reply immediately, start the server after.
        var ackPayload = JsonConvert.SerializeObject(new
        {
            type = 4, // CHANNEL_MESSAGE_WITH_SOURCE
            data = new { content = $"⏳ Starting **{serverName}**... give it a minute, then connect normally.", flags = 64 } // 64 = ephemeral
        });
        try
        {
            await _http.PostAsync(
                $"https://discord.com/api/v10/interactions/{id}/{token}/callback",
                new StringContent(ackPayload, Encoding.UTF8, "application/json"), ct);
        }
        catch { }

        if (server != null)
            await (StartServer?.Invoke(server.Id) ?? Task.CompletedTask);
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
                ServerStatus.Stopped    => "⚫",
                ServerStatus.Starting   => "🔵",
                ServerStatus.Stopping   => "🔵",
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
