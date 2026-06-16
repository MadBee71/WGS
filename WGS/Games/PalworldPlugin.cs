using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class PalworldPlugin : GamePluginBase, IWorkshopPlugin, IRestPlayersPlugin
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
    public override int    DefaultPort      => 8211;
    public override int    DefaultQueryPort => 8212;
    public override int    DefaultMaxPlayers => 32;
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
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverDescription", Label = "Description",   FieldType = ConfigFieldType.Text,     DefaultValue = "Palworld Server" },
            new() { Key = "adminPassword",     Label = "Admin password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }

    // ── REST API player list ──────────────────────────────────────────────────
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public string GetRestApiBaseUrl(GameServer server)
        => $"http://127.0.0.1:{server.QueryPort}";

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
            if (!resp.IsSuccessStatusCode) return [];
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
                        Ping    = p.TryGetProperty("ping",    out var pg) ? pg.GetInt32() : 0,
                    });
                }
            }
            return players;
        }
        catch { return []; }
    }
}
