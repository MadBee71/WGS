using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class NightingalePlugin : GamePluginBase, IRestPlayersPlugin
{
    public override string GameId          => "nightingale";
    public override string GameName        => "Nightingale";
    public override string Description     => "Victorian gaslamp fantasy co-op survival game";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3796810; // dedicated server tool — different from the game's own appid
    public override int    GameStoreAppId  => 1928980;
    public override string Executable      => "NWXServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 6;

    public override string BuildStartArguments(GameServer s)
        => $"-log -port={s.ServerPort} -statusPort={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();

    // ── REST status endpoint ────────────────────────────────────────────────
    // "-statusPort" starts a plain HTTP JSON status endpoint (GET /status ->
    // {"status":"ready","player_count":N,"player_names":[...]}), NOT a Source
    // A2S query port. Querying it with A2S (as this plugin used to) never gets
    // a valid reply, so both the player count and the health-check freeze
    // detector always read it as down.
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public string GetRestApiBaseUrl(GameServer server)
        => $"http://127.0.0.1:{(server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort)}";

    public string? LastRestApiError { get; private set; }

    public async Task<List<OnlinePlayer>> GetPlayersAsync(GameServer server)
    {
        try
        {
            var resp = await _http.GetAsync($"{GetRestApiBaseUrl(server)}/status");
            if (!resp.IsSuccessStatusCode)
            {
                LastRestApiError = $"HTTP {(int)resp.StatusCode} {resp.StatusCode} from {GetRestApiBaseUrl(server)}/status";
                return [];
            }
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var players = new List<OnlinePlayer>();
            if (doc.RootElement.TryGetProperty("player_names", out var arr))
            {
                foreach (var p in arr.EnumerateArray())
                    players.Add(new OnlinePlayer { Name = p.GetString() ?? "" });
            }
            LastRestApiError = null;
            return players;
        }
        catch (Exception ex)
        {
            LastRestApiError = $"{ex.GetType().Name}: {ex.Message} (status endpoint reachable at {GetRestApiBaseUrl(server)}/status?)";
            return [];
        }
    }
}
