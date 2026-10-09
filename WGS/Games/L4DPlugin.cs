using WGS.Models;

namespace WGS.Games;

public class L4DPlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "l4d";
    public override string GameName        => "Left 4 Dead";
    public override string Description     => "Valve's original four-player zombie co-op shooter";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 222840;
    public override int    GameStoreAppId  => 500;
    public override string Executable      => "srcds.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 8;
    // Verified 9.10.2026: the dedicated server (app 222840) downloads with an anonymous SteamCMD login.
    public override bool   RequiresSteamLogin => false;
    public override bool   HasRcon         => true;
    public override bool   SupportsSourceMod  => true;
    public override string SourceModGameDir => "left4dead";

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
        WriteSrcdsServerCfg(s, "left4dead");
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "l4d_hospital01_apartment");
        return $"-game left4dead -console -usercon{SrcdsIpArg(s)} +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers}{SrcdsRconArg(s)}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "l4d_hospital01_apartment",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "l4d_hospital01_apartment",
                    Options = ["l4d_hospital01_apartment", "l4d_farm01_hilltop", "l4d_smalltown01_caves", "l4d_airport01_greenhouse", "l4d_death_toll01_woods"] },
        ]);
        return fields;
    }
}
