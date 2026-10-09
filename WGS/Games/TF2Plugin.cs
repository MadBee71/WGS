using WGS.Models;

namespace WGS.Games;

public class TF2Plugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "tf2";
    public override string GameName        => "Team Fortress 2";
    public override string Description     => "Valve's iconic team-based multiplayer FPS";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 232250;
    public override int    GameStoreAppId  => 440;
    public override string Executable      => "srcds.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 24;
    // Verified 9.10.2026: the dedicated server (app 232250) downloads with an anonymous SteamCMD login.
    public override bool   RequiresSteamLogin => false;
    public override bool   HasRcon         => true;
    public override bool   SupportsSourceMod  => true;
    public override string SourceModGameDir => "tf";

        public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();
    // srcds aborts at startup with an "Engine Error" dialog (CTextConsoleWin32::GetLine:
    // !GetNumberOfConsoleInputEvents) when its stdin is a pipe, which is how WGS launches a game by
    // default — verified against real srcds servers (L4D2, HL2DM). So it runs in its own console window
    // (native console) and is stopped over RCON ("quit"); srcds serves RCON on the game port itself.
    public override bool      UseNativeConsole                      => true;
    public override string?   GetStopCommand(GameServer server)     => null;
    public override string[]? GetRconStopCommand(GameServer server) => ["quit"];
    public override int       GetRconPort(GameServer server)        => server.ServerPort;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override Task PreStartAsync(GameServer s)
    {
        WriteSrcdsServerCfg(s, "tf");
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "cp_dustbowl");
        return $"-game tf -console -usercon{SrcdsIpArg(s)} +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers}{SrcdsRconArg(s)}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "cp_dustbowl",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "cp_dustbowl",
                    Options = ["cp_dustbowl", "pl_badwater", "ctf_2fort", "koth_harvest_final", "cp_steel"] },
        ]);
        return fields;
    }
}
