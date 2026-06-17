using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class PalworldPlugin : GamePluginBase, IWorkshopPlugin, IRestPlayersPlugin, IRestCommandPlugin
{
    public override string GameId          => "palworld";
    public override string GameName        => "Palworld";
    public override string Description     => "Survival crafting with creature collecting";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2394010;
    public override int    GameStoreAppId  => 1623730;
    public override int    WorkshopAppId   => 2394010;

    public string ModTargetDirectory => @"Pal/Content/Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"Pal\Binaries\Win64\PalServer-Win64-Shipping-Cmd.exe";
    public override bool   HasRcon          => true;
    // PalServer's "-Cmd" build expects a real Windows console, not a redirected pipe.
    // Redirected stdio has been linked to intermittent "SECURE CRT: Invalid parameter
    // detected" engine crashes after running for a while. Give it its own native console
    // window instead (same approach as Wreckfest/Wreckfest2).
    public override bool   UseNativeConsole => true;
    public override int    DefaultPort      => 8211;
    public override int    DefaultQueryPort => 8212;
    public override int    DefaultMaxPlayers => 32;
    public override string? GetBroadcastCommand(string message) => $"broadcast {message}";
    public override string BuildStartArguments(GameServer s)
    {
        var args = $"-port={s.ServerPort} -queryport={s.QueryPort} -players={s.MaxPlayers} " +
                   $"EpicApp=PalServer -useperfthreads -NoAsyncLoadingThread -UseMultithreadForDS";
        if (s.RconPort > 0 && !string.IsNullOrWhiteSpace(s.RconPassword))
            args += $" -RCONEnabled=True -RCONPort={s.RconPort}";
        return args;
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverDescription"] = "Palworld Server",
        ["adminPassword"]     = "",
        ["restApiPort"]       = "21512",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverDescription", Label = "Description",    FieldType = ConfigFieldType.Text,     DefaultValue = "Palworld Server" },
            new() { Key = "adminPassword",     Label = "Admin password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
            new() { Key = "restApiPort",       Label = "REST API port",  FieldType = ConfigFieldType.Number,   DefaultValue = "21512" },
        ]);
        return fields;
    }

    // ── REST API player list ──────────────────────────────────────────────────
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private int RestApiPort(GameServer server)
    {
        var p = S(server, "restApiPort", "21512");
        return int.TryParse(p, out var n) ? n : 21512;
    }

    public string GetRestApiBaseUrl(GameServer server)
        => $"http://127.0.0.1:{RestApiPort(server)}";

    public string? LastRestApiError { get; private set; }

    // Palworld's REST API sometimes returns ping as a float (e.g. 12.5) rather than an int.
    private static int GetPingAsInt(JsonElement pg) => pg.TryGetInt32(out var i) ? i : (int)pg.GetDouble();

    public async Task<List<OnlinePlayer>> GetPlayersAsync(GameServer server)
    {
        var password = S(server, "adminPassword", "");
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{GetRestApiBaseUrl(server)}/v1/api/players");
            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:" + password));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                LastRestApiError = $"HTTP {(int)resp.StatusCode} {resp.StatusCode} from {req.RequestUri} " +
                                    "(check AdminPassword in PalWorldSettings.ini matches 'Admin password' in WGS Game settings)";
                return [];
            }
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var players = new List<OnlinePlayer>();
            if (doc.RootElement.TryGetProperty("players", out var arr))
            {
                foreach (var p in arr.EnumerateArray())
                {
                    players.Add(new OnlinePlayer
                    {
                        Name    = p.TryGetProperty("name",    out var n) ? n.GetString() ?? "" : "",
                        SteamId = p.TryGetProperty("userId",  out var u) ? u.GetString() ?? "" : "",
                        Ping    = p.TryGetProperty("ping", out var pg) ? GetPingAsInt(pg) : 0,
                    });
                }
            }
            LastRestApiError = null;
            return players;
        }
        catch (Exception ex)
        {
            LastRestApiError = $"{ex.GetType().Name}: {ex.Message} (REST API reachable at {GetRestApiBaseUrl(server)}?)";
            return [];
        }
    }

    // ── REST API console commands ───────────────────────────────────────────
    // PalServer's stdin-based admin commands (Broadcast/Save/Shutdown/KickPlayer/etc.)
    // are no longer reachable now that the server runs with a native console window.
    // These are the same commands PalServer always accepted via stdin, just sent through
    // the REST API instead so "!cmd" / web dashboard console commands keep working.
    public async Task<(bool handled, string response)> TrySendRestCommandAsync(GameServer server, string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts[0].Length == 0) return (false, "");
        var verb = parts[0];
        var rest = parts.Length > 1 ? parts[1] : "";

        try
        {
            switch (verb.ToLowerInvariant())
            {
                case "broadcast":
                    return (true, await PostAsync(server, "announce", new { message = rest }));

                case "save":
                    return (true, await PostAsync(server, "save", new { }));

                case "shutdown":
                    {
                        var p = rest.Split(' ', 2, StringSplitOptions.TrimEntries);
                        int waitSec = p.Length > 0 && int.TryParse(p[0], out var s) ? s : 0;
                        var msg = p.Length > 1 ? p[1] : "Server is shutting down";
                        return (true, await PostAsync(server, "shutdown", new { waittime = waitSec, Message = msg }));
                    }

                case "doexit":
                case "stop":
                    return (true, await PostAsync(server, "stop", new { }));

                case "kickplayer":
                    return (true, await PostAsync(server, "kick", new { userid = rest, message = "Kicked by admin" }));

                case "banplayer":
                    return (true, await PostAsync(server, "ban", new { userid = rest, message = "Banned by admin" }));

                case "unbanplayer":
                    return (true, await PostAsync(server, "unban", new { userid = rest }));

                default:
                    return (false, "");
            }
        }
        catch (Exception ex)
        {
            return (true, $"[REST API] {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task<string> PostAsync(GameServer server, string endpoint, object body)
    {
        var password = S(server, "adminPassword", "");
        var req = new HttpRequestMessage(HttpMethod.Post, $"{GetRestApiBaseUrl(server)}/v1/api/{endpoint}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:" + password));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode
            ? $"[REST API] OK: {endpoint}"
            : $"[REST API] HTTP {(int)resp.StatusCode} {resp.StatusCode} from {endpoint}";
    }
}
