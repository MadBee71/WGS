using WGS.Models;

namespace WGS.Games;

public class ArkSurvivalAscendedPlugin : GamePluginBase, IWipePlugin, IA2SQueryPlugin
{
    public override string GameId          => "arksurvivalascended";
    public override string GameName        => "ARK: Survival Ascended";
    public override string Description     => "Unreal Engine 5 remaster of ARK: Survival Evolved";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2430930;
    public override int    GameStoreAppId  => 2399830;
    public override string Executable      => @"ShooterGame\Binaries\Win64\ArkAscendedServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 40;
    public override bool   HasRcon           => true;

    public override string  EngineFamily                                     => ArkRcon.Family;
    public override string? GetKickCommand(string p)                         => ArkRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => ArkRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => ArkRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => ArkRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => ArkRcon.Unban(p);
    public override string? GetPlayersCommand()                              => ArkRcon.Players();

    // Same documented graceful sequence as ARK: Survival Evolved (ARKSEPlugin) — SaveWorld first
    // because DoExit alone isn't guaranteed to save. Previously entirely absent (no RCON wiring at
    // all), so Stop always hard-killed ASA — unlike ARKSE, whose sibling plugin already had this.
    public override string[]? GetRconStopCommand(GameServer server) => ["SaveWorld", "DoExit"];

    // IWipePlugin
    public IEnumerable<string> GetMapWipePaths(GameServer server)
    {
        var map = server.GameSpecificSettings.TryGetValue("mapName", out var m) ? m : "TheIsland_WP";
        return [$@"ShooterGame\Saved\SavedArks\{map}.ark",
                $@"ShooterGame\Saved\SavedArks\*.arktribe",
                $@"ShooterGame\Saved\SavedArks\*.arkprofile"];
    }

    public IEnumerable<string> GetFullWipePaths(GameServer server) => GetMapWipePaths(server);


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "mapName", "TheIsland_WP");
        var args = $"{map}?listen?SessionName=\"{s.ServerName}\"?Port={s.ServerPort}?QueryPort={s.QueryPort}?MaxPlayers={s.MaxPlayers}";
        // RCON only turns on when the user has actually set an RCON/admin password — ARK's
        // ServerAdminPassword doubles as the RCON password, and enabling RCON without one would
        // leave it unauthenticated. Port defaults to ServerPort+10 to match the fallback
        // ServerManagerService.TrySendRconCommandAsync already uses when RconPort isn't set, so
        // GetRconStopCommand above actually connects to the port the server is really listening on.
        // Verified against ARK:SA's own startup-parameter docs (XGamingServer) — same
        // ?-delimited RCONEnabled/RCONPort/ServerAdminPassword syntax as ARK:SE.
        if (!string.IsNullOrWhiteSpace(s.RconPassword))
        {
            var rconPort = s.RconPort > 0 ? s.RconPort : s.ServerPort + 10;
            args += $"?RCONEnabled=True?RCONPort={rconPort}?ServerAdminPassword={s.RconPassword}";
        }
        return args;
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mapName"] = "TheIsland_WP",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "mapName", Label = "Map", FieldType = ConfigFieldType.Dropdown,
            DefaultValue = "TheIsland_WP",
            Options = ["TheIsland_WP", "ScorchedEarth_WP", "Aberration_WP", "Extinction_WP", "Genesis_WP", "Gen2_WP", "Svartalfheim_WP"] });
        return fields;
    }
}
