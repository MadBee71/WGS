using System.IO;
using System.Linq;
using WGS.Models;

namespace WGS.Games;

// Full rewrite 19.9.2026 — the original plugin (never released/used) had several unverified,
// fabricated details: DefaultMaxPlayers=16 (game hard-caps at 6), a "-QueryPort"/"-ini:...MaxPlayers"
// launch flag that doesn't exist in any source, a wrong GameStoreAppId (1510330 — the real Steam
// store page is app 1374490), and no config-file handling at all even though the game's own
// DedicatedServer.ini is *mandatory* (won't start without OwnerId set). Rebuilt from two
// independent, agreeing sources: the official "How to: Dedicated Servers" news post
// (dragonwilds.runescape.com/news/how-to-dedicated-servers) and the community Unofficial Wreckfest-
// style wiki page (dragonwilds.runescape.wiki/w/Dedicated_Servers), cross-checked against the
// published darkharasho/WindowsGSM.RunescapeDragonwilds plugin for the executable-path and
// working-INI-key details neither wiki page spells out precisely.
public class RunescapeDragonwildsPlugin : GamePluginBase
{
    public override string GameId          => "runescapedragonwilds";
    public override string GameName        => "RuneScape: Dragonwilds";
    public override string Description     => "Co-op survival crafting game set in the RuneScape universe";
    public override string Category        => "Survival";
    // 4019830 is the SEPARATE, free "RuneScape Dragonwilds: Dedicated Server" SteamCMD depot —
    // not the paid base game (1374490, used below only for the store capsule image) — so
    // anonymous SteamCMD login works and RequiresSteamLogin is correctly left at its default false.
    public override int    SteamAppId      => 4019830;
    public override int    GameStoreAppId  => 1374490;
    // RSDragonwilds.exe in the install root is a launcher stub that starts this binary and exits
    // immediately — pointing WGS at the stub would make it think the server crashed right after
    // launch. Confirmed via WindowsGSM's plugin, same reasoning already applied to every other
    // Unreal Engine game in this codebase (ARK, Conan, etc. — always the real Binaries\Win64 exe).
    public override string Executable       => @"RSDragonwilds\Binaries\Win64\RSDragonwildsServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    // No query/A2S protocol exists for this game (confirmed by both wiki sources — "no query port
    // is documented" — and by the WindowsGSM plugin's own comment: "Dragonwilds does not expose an
    // A2S/Steam query endpoint"). This value is never sent to the game or queried; it only exists
    // so a second server on the same machine reserves a distinct port, matching the docs' own
    // "second server will use 7778" example.
    public override int    DefaultQueryPort => 7778;
    // Hard-capped by the game itself ("Max Players: 6 as of version 0.11" — not moddable via any
    // known flag or ini key) — not a WGS-configurable value, see the locked Min/Max below.
    public override int    DefaultMaxPlayers => 6;

    // No known stdin/RCON/REST stop command exists for this game — Stop falls through to a hard
    // kill. A Ctrl+C-forward-to-console mechanism was prototyped and tested 19.9.2026 as a possible
    // fix but proved unreliable in-process (see project_wgs_ctrlc_stop_investigation_2026-09-19.md)
    // — not implemented. Kept on WGS's normal redirected/embedded console (below) rather than
    // switching to a native console window for no benefit, since that would only have been worth
    // the trade-off if Ctrl+C-forwarding actually worked.
    //
    // Only "-port=" is a documented launch argument; "-log" is the vendor-recommended flag for
    // visible server activity output. "-NewConsole" (also suggested by the docs) intentionally
    // left out — it opens a separate native console window, which would break WGS's redirected-
    // stdout capture the same way UseNativeConsole does for other games.
    public override string BuildStartArguments(GameServer s) => $"-port={s.ServerPort} -log";

    // DedicatedServer.ini is mandatory — the game refuses to start without OwnerId set. Path and
    // section header confirmed by both wiki sources and the WindowsGSM plugin's actual working
    // implementation. Merges rather than overwrites (same pattern as StationeersPlugin's
    // default.ini writer): known keys are updated in place from WGS's own settings on every start,
    // anything else already in the file is left untouched.
    public override async Task PreStartAsync(GameServer server)
    {
        var iniPath = Path.Combine(server.InstallPath, "RSDragonwilds", "Saved", "Config", "WindowsServer", "DedicatedServer.ini");
        var worldName = S(server, "defaultWorldName", "My World");
        if (string.IsNullOrWhiteSpace(worldName)) worldName = "My World";
        var serverName = string.IsNullOrWhiteSpace(server.ServerName) ? "RuneScape Dragonwilds Server" : server.ServerName;

        var wanted = new Dictionary<string, string>
        {
            ["OwnerId"]          = S(server, "ownerId", ""),
            ["ServerName"]       = serverName,
            ["DefaultWorldName"] = worldName,
            ["AdminPassword"]    = S(server, "adminPassword", ""),
            ["WorldPassword"]    = server.ServerPassword,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(iniPath)!);
        const string sectionHeader = "[/Script/Dominion.DedicatedServerSettings]";
        var lines = File.Exists(iniPath) ? (await File.ReadAllLinesAsync(iniPath)).ToList() : new List<string> { sectionHeader };
        if (!lines.Any(l => l.Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase)))
            lines.Insert(0, sectionHeader);

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('[') || trimmed.StartsWith(';')) continue;
            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;
            var key = trimmed[..eq].Trim();
            if (wanted.Remove(key, out var value))
                lines[i] = $"{key}={value}";
        }
        var sectionIdx = lines.FindIndex(l => l.Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase));
        var insertAt = sectionIdx + 1;
        while (insertAt < lines.Count && !lines[insertAt].Trim().StartsWith('[')) insertAt++;
        foreach (var (key, value) in wanted)
            lines.Insert(insertAt++, $"{key}={value}");

        await File.WriteAllLinesAsync(iniPath, lines);
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["ownerId"]          = "",
        ["defaultWorldName"] = "My World",
        ["adminPassword"]    = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();

        var maxPlayers = fields.First(f => f.Key == "maxPlayers");
        maxPlayers.Min = 6;
        maxPlayers.Max = 6;
        maxPlayers.Description = "Hard-capped at 6 players by the game itself — not configurable.";

        var password = fields.First(f => f.Key == "serverPass");
        password.Label = "World password";
        password.Description = "Sets WorldPassword in DedicatedServer.ini. Leave empty for an open world.";

        fields.AddRange([
            new() { Key = "ownerId", Label = "Owner Player ID", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "Required — find this at the bottom of the in-game Settings menu. The server will not start until this is set." },
            new() { Key = "defaultWorldName", Label = "Default world name", FieldType = ConfigFieldType.Text, DefaultValue = "My World" },
            new() { Key = "adminPassword", Label = "Admin password", FieldType = ConfigFieldType.Password, DefaultValue = "",
                    Description = "Grants Server Management access in-game to anyone who knows it. Leave empty to disable." },
        ]);
        return fields;
    }
}
