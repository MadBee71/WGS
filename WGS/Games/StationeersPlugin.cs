using System.IO;
using System.Net;
using System.Net.Http;
using WGS.Models;

namespace WGS.Games;

public class StationeersPlugin : GamePluginBase, IA2SQueryPlugin, IRestCommandPlugin
{
    public override string GameId          => "stationeers";
    public override string GameName        => "Stationeers";
    public override string Description     => "Space station building and survival simulation";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 600760;
    public override int    GameStoreAppId  => 544550;
    public override string Executable      => "rocketstation_DedicatedServer.exe";
    public override int    DefaultPort      => 27500;
    // The game's own docs explicitly warn against 27015-27020 for -gameport because those are
    // Steam's local server query ports — the plugin's old default (27015) sat right inside that
    // reserved range. Moved well clear of it.
    public override int    DefaultQueryPort => 27050;
    public override int    DefaultMaxPlayers => 16;
    // Headless Unity (-nographics -batchmode) runs against a fake "Null" graphics device and
    // logs a "Shader X is not supported on this GPU" line for every shader it tries to validate
    // against it — hundreds of them, all harmless, not a sign the server needs a real GPU
    // (confirmed live: Jack Daniels, Discord forum, 18.9.2026 — server reached "Ready" and ran
    // fine despite the flood). Same noise class already filtered for Valheim/Rust/SCUM etc.
    protected override bool FilterUnityShaderNoise => true;

    // Full rewrite 18.9.2026 — the plugin previously sent only "-batchmode -nographics -port N"
    // and never actually configured anything, so the server ran with no real name/password/limits
    // and looked for its settings somewhere WGS never controlled (reported by Jack Daniels,
    // Discord forum "Stationeers Failed", 18.9.2026). Two rounds of research gave contradictory
    // command-line syntax from different community sources (an older "-settingspath/-settings
    // Key Value" guide vs. the flags below) — this final version is built from the user's own
    // screenshot of the actual Stationeers wiki "Dedicated Server Commands" page, a first-party,
    // unambiguous source, and supersedes everything found earlier that conflicted with it.
    //
    // Real flags: -gameport, -updateport (Steam query port — separate from RCON), -loadworld,
    // -worldtype, -autosaveinterval, -servername, -basedirectory. Password/max-players/RCON
    // password have no CLI flag at all — they only exist in default.ini, written below in
    // PreStartAsync ("Command parameters override default.ini" per the wiki, so the CLI flags
    // above still win for the fields that have one).
    public override string BuildStartArguments(GameServer s)
    {
        var world        = S(s, "worldName", "BASE");
        if (string.IsNullOrWhiteSpace(world)) world = "BASE";
        var worldType    = S(s, "worldType", "Mars");
        var saveInterval = int.TryParse(S(s, "autoSaveInterval", "300"), out var si) && si > 0 ? si : 300;
        var name         = string.IsNullOrWhiteSpace(s.ServerName) ? "Stationeers" : s.ServerName;

        return $"-batchmode -nographics -autostart -gameport {s.ServerPort} -updateport {s.QueryPort} " +
               $"-loadworld \"{world}\" -worldtype {worldType} -autosaveinterval {saveInterval} " +
               $"-servername \"{name}\" -basedirectory \"{s.InstallPath}\"";
    }

    // Merges the fields that have no command-line equivalent — ServerPassword, MaxPlayers, and
    // critically the RCON password (the wiki confirms that one can ONLY be set here, never via a
    // flag) — into default.ini under -basedirectory. Merges rather than overwrites: unfamiliar
    // keys this plugin doesn't set (MAPNAME, DESCRIPTION, and whatever else the game itself first
    // generates the file with) are left exactly as found rather than guessed at or wiped out.
    public override async Task PreStartAsync(GameServer server)
    {
        var iniPath    = Path.Combine(server.InstallPath, "default.ini");
        var name       = string.IsNullOrWhiteSpace(server.ServerName) ? "Stationeers" : server.ServerName;
        var maxPlayers = Math.Clamp(server.MaxPlayers, 1, 30);
        var rconPass   = S(server, "authSecret", "");

        var wanted = new Dictionary<string, Dictionary<string, string>>
        {
            ["SERVER"] = new()
            {
                ["SERVERNAME"]  = name,
                ["GAMEPORT"]    = server.ServerPort.ToString(),
                ["UPDATERPORT"] = server.QueryPort.ToString(),
                ["PASSWORD"]    = server.ServerPassword,
                ["MAXPLAYER"]   = maxPlayers.ToString(),
            },
            ["RCON"] = new() { ["RCONPASSWORD"] = rconPass },
        };

        var lines = File.Exists(iniPath) ? (await File.ReadAllLinesAsync(iniPath)).ToList() : [];
        string? currentSection = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1].ToUpperInvariant();
                continue;
            }
            if (currentSection == null || !wanted.TryGetValue(currentSection, out var sectionKeys)) continue;
            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;
            var key = trimmed[..eq].Trim().ToUpperInvariant();
            if (sectionKeys.Remove(key, out var value))
                lines[i] = $"{key}={value}"; // overwrite in place, keep original position
        }
        // Anything left in `wanted` wasn't found in the existing file — append it (creating the
        // section header too if the file didn't have one yet).
        foreach (var (section, keys) in wanted)
        {
            if (keys.Count == 0) continue;
            if (!lines.Any(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase)))
                lines.Add($"[{section}]");
            var sectionIdx = lines.FindIndex(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));
            var insertAt = sectionIdx + 1;
            while (insertAt < lines.Count && !lines[insertAt].Trim().StartsWith('[')) insertAt++;
            foreach (var (key, value) in keys)
                lines.Insert(insertAt++, $"{key}={value}");
        }
        await File.WriteAllLinesAsync(iniPath, lines);
    }

    // ── A2S query ────────────────────────────────────────────────────────────
    // -updateport is described directly as "UDP port for steam query" — the earlier removal of
    // A2S support was wrong about WHY it failed: WGS never told the game what port to listen on
    // for queries (BuildStartArguments passed nothing), so it queried a port nothing was bound
    // to. Now that -updateport is wired to the same QueryPort WGS tracks, A2S is back.
    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;

    // ── HTTP admin API (the wiki calls it "RCON"; it's actually a small web console) ──────────
    // GET http://<host>:<gameport>/console/run?command=<url-escaped>, session-cookie auth via
    // "login <password>" first (confirmed against matjam/stationeersrcon's Go source and
    // Didstopia/stationeers-server independently, and now matches the wiki's own worked example
    // showing the same login → command flow). Own "Admin password" field rather than the base
    // RconPassword/HasRcon pair — those drive the Console tab's RCON-connect button and WGS's
    // Source-binary RconService, neither of which speak this game's HTTP protocol; turning HasRcon
    // on would add a connect button that always fails instead of just not being there.
    //
    // "save <worldname>" now has a fully specified, safe target: the wiki's own example shows it
    // saves to "<basedirectory>\<worldname>", so passing the SAME name used for -loadworld above
    // overwrites the actual running world rather than guessing at an unrelated filename.
    public async Task<(bool handled, string response)> TrySendRestCommandAsync(GameServer server, string command)
    {
        if (command.Trim().ToLowerInvariant() is not ("stop" or "shutdown")) return (false, "");
        var authSecret = S(server, "authSecret", "");
        if (string.IsNullOrWhiteSpace(authSecret))
            return (true, "[Admin API] No admin password set for this server (Settings → Admin password) — can't log in.");

        try
        {
            var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            var baseUrl = $"http://127.0.0.1:{server.ServerPort}";

            var login = await http.GetAsync($"{baseUrl}/console/run?command={Uri.EscapeDataString($"login {authSecret}")}");
            if (!login.IsSuccessStatusCode)
                return (true, $"[Admin API] Login failed: HTTP {(int)login.StatusCode} {login.StatusCode}");

            var world = S(server, "worldName", "BASE");
            if (string.IsNullOrWhiteSpace(world)) world = "BASE";
            await http.GetAsync($"{baseUrl}/console/run?command={Uri.EscapeDataString($"save \"{world}\"")}");
            await Task.Delay(2000);
            await http.GetAsync($"{baseUrl}/console/run?command={Uri.EscapeDataString("shutdown")}");
            return (true, "[Admin API] Save + shutdown sent.");
        }
        catch (Exception ex)
        {
            return (true, $"[Admin API] {ex.GetType().Name}: {ex.Message}");
        }
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["worldName"]         = "BASE",
        ["worldType"]         = "Mars",
        ["autoSaveInterval"]  = "300",
        ["authSecret"]        = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName",        Label = "World/save name", FieldType = ConfigFieldType.Text,     DefaultValue = "BASE" },
            new() { Key = "worldType",        Label = "World type",      FieldType = ConfigFieldType.Dropdown, DefaultValue = "Mars", Options = ["Space", "Mars", "Terrain"] },
            new() { Key = "autoSaveInterval", Label = "Auto-save interval (seconds)", FieldType = ConfigFieldType.Number, DefaultValue = "300" },
            new() { Key = "authSecret",       Label = "Admin password",  FieldType = ConfigFieldType.Password, DefaultValue = "",
                    Description = "Sets RCONPASSWORD in default.ini, enabling the server's built-in web admin console and letting WGS send a graceful save+shutdown instead of a hard kill. Leave empty to leave it off." },
        ]);
        return fields;
    }
}
