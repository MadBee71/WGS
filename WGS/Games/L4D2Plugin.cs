using System.IO;
using System.Text.RegularExpressions;
using WGS.Models;

namespace WGS.Games;

public class L4D2Plugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId           => "l4d2";
    public override string GameName         => "Left 4 Dead 2";
    public override string Description      => "Left 4 Dead 2 co-op zombie survival server";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 222860;
    public override int    GameStoreAppId   => 550;
    public override string Executable       => "srcds.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 8;
    public override bool   HasRcon          => true;
    public override bool   SupportsSourceMod  => true;
    public override string SourceModGameDir => "left4dead2";
    // The L4D2 dedicated server (app 222860) downloads with an anonymous SteamCMD login. It used to
    // say true here, which made WGS refuse to install it without stored Steam credentials.
    public override bool   RequiresSteamLogin => false;

    // srcds aborts right at startup with an "Engine Error" dialog ("CTextConsoleWin32::GetLine:
    // !GetNumberOfConsoleInputEvents") when its stdin is a pipe — which is how WGS launches a game
    // by default — and then sits there waiting for someone to click OK. Verified against the real
    // server: with a real console it starts in seconds. So it runs in its own console window (like
    // Wreckfest/Palworld) and is stopped over RCON ("quit"), since stdin isn't available.
    public override bool     UseNativeConsole                                  => true;
    public override string?  GetStopCommand(GameServer server)                 => null;
    public override string[]? GetRconStopCommand(GameServer server)            => ["quit"];
    // srcds serves RCON (TCP) on the game port itself, not game port + 10 like most games.
    public override int      GetRconPort(GameServer server)                    => server.ServerPort;

    public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();

    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override Task PreStartAsync(GameServer s)
    {
        var cfg = Path.Combine(s.InstallPath, "left4dead2", "cfg", "server.cfg");
        WriteConfigIfMissing(cfg, SourceCfg(s));
        return Task.CompletedTask;
    }

    // Maps per mode, taken from the real dedicated server's own files (not guessed):
    //  - campaign start maps: all of them load (checked by starting the server on each one)
    //  - survival: the survival maps defined in the game's missions/campaign*.txt (campaign start maps
    //    such as c1m1_hotel are NOT survival maps)
    //  - scavenge: the maps whose BSP contains weapon_scavenge_item_spawn entities
    private static readonly string[] CampaignMaps =
    [
        "c1m1_hotel", "c2m1_highway", "c3m1_plankcountry", "c4m1_milltown_a", "c5m1_waterfront", "c6m1_riverbank", "c7m1_docks",
        "c8m1_apartment", "c9m1_alleys", "c10m1_caves", "c11m1_greenhouse", "c12m1_hilltop", "c13m1_alpinecreek",
    ];
    private static readonly string[] SurvivalMaps = ["c1m4_atrium", "c2m1_highway", "c3m1_plankcountry", "c4m1_milltown_a", "c5m2_park"];
    private static readonly string[] ScavengeMaps =
    [
        "c1m4_atrium", "c2m1_highway", "c3m1_plankcountry", "c4m1_milltown_a", "c4m2_sugarmill_a", "c4m3_sugarmill_b", "c5m2_park",
        "c6m1_riverbank", "c6m2_bedlam", "c6m3_port", "c7m1_docks", "c7m2_barge", "c8m1_apartment", "c8m5_rooftop",
        "c9m1_alleys", "c10m3_ranchhouse", "c11m4_terminal", "c12m5_cornfield",
    ];

    private static readonly Regex SafeMapName = new("^[A-Za-z0-9_\\-]+$", RegexOptions.Compiled);

    private string ResolveMap(GameServer s, string mode)
    {
        // A free-text override for any map not in the lists (add-on campaigns, newer maps) — only a plain
        // map name is accepted, since it ends up on the command line.
        var custom = S(s, "customMap", "").Trim();
        if (custom.Length > 0 && SafeMapName.IsMatch(custom)) return custom;
        return mode switch
        {
            "survival" => S(s, "survivalMap", SurvivalMaps[0]),
            "scavenge" => S(s, "scavengeMap", ScavengeMaps[0]),
            _          => S(s, "map", CampaignMaps[0]),   // coop, versus, realism
        };
    }

    public override string BuildStartArguments(GameServer s)
    {
        var mode = S(s, "mode", "coop");
        var map  = ResolveMap(s, mode);
        // Always pass -ip (see SrcdsIpArg): without it srcds listens on one adapter only, so WGS's loopback RCON/A2S can't reach it.
        // The game mode must be set with +mp_gamemode: a bare mode word after the map ("+map X versus") is
        // ignored by the server (verified: it stayed co-op with 4 slots instead of 8).
        return $"-game left4dead2 -console -usercon{SrcdsIpArg(s)} +map {map} +mp_gamemode {mode} -port {s.ServerPort} +maxplayers {s.MaxPlayers}{SrcdsRconArg(s)}";
    }

    // sv_allow_lobby_connect_only 0: L4D2 dedicated servers otherwise only accept players coming
    // through a matchmaking lobby, not direct connects/the server browser. sv_lan 0 = internet server.
    // Only written for new servers (WriteConfigIfMissing) — an existing server.cfg is never touched.
    private static string SourceCfg(GameServer s) =>
        $"""
        hostname "{s.ServerName}"
        sv_password "{s.ServerPassword}"
        rcon_password "{s.RconPassword}"
        sv_lan 0
        sv_allow_lobby_connect_only 0
        """;

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mode"]        = "coop",
        ["map"]         = CampaignMaps[0],
        ["survivalMap"] = SurvivalMaps[0],
        ["scavengeMap"] = ScavengeMaps[0],
        ["customMap"]   = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "mode", Label = "Mode", FieldType = ConfigFieldType.Dropdown, DefaultValue = "coop",
                Options = ["coop","versus","survival","scavenge","realism"] },
            new() { Key = "map",  Label = "Map (co-op / versus / realism)", FieldType = ConfigFieldType.Dropdown, DefaultValue = CampaignMaps[0],
                Options = CampaignMaps, Description = "Starting map for the campaign modes (co-op, versus, realism)." },
            new() { Key = "survivalMap", Label = "Map (survival)", FieldType = ConfigFieldType.Dropdown, DefaultValue = SurvivalMaps[0],
                Options = SurvivalMaps, Description = "Used when Mode is Survival. Survival has its own maps — campaign start maps don't work for it." },
            new() { Key = "scavengeMap", Label = "Map (scavenge)", FieldType = ConfigFieldType.Dropdown, DefaultValue = ScavengeMaps[0],
                Options = ScavengeMaps, Description = "Used when Mode is Scavenge." },
            new() { Key = "customMap", Label = "Custom map (optional)", FieldType = ConfigFieldType.Text, DefaultValue = "",
                Description = "Any other map name (e.g. an add-on campaign). When filled in, it replaces the map chosen above." },
        ]);
        return fields;
    }
}
