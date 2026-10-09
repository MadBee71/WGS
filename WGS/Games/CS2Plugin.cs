using WGS.Models;

namespace WGS.Games;

public class CS2Plugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId          => "cs2";
    public override string GameName        => "Counter-Strike 2";
    public override string Description     => "Valve's tactical FPS — CS2 dedicated server";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 730;
    public override int    GameStoreAppId  => 730;
    public override int    WorkshopAppId   => 730;
    public override string Executable      => @"game\bin\win64\cs2.exe";

    public string ModTargetDirectory => @"game\csgo\addons";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 10;
    public override bool   HasRcon         => true;
    public override bool   SupportsSourceMod  => true;
    public override string SourceModGameDir => "game/csgo";

        public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();
    // cs2.exe's console fails with "CTextConsoleWin::GetLine: !GetNumberOfConsoleInputEvents" (spammed endlessly in the WGS
    // console) when its stdin is a pipe, which is how WGS launches a game by default — the same fault as the other Source-engine
    // servers (verified on L4D2 and HL2DM). So it runs in its own console window (native console) and is stopped over RCON
    // ("quit"); the server serves RCON on the game port itself.
    public override bool      UseNativeConsole                      => true;
    public override string?   GetStopCommand(GameServer server)     => null;
    public override string[]? GetRconStopCommand(GameServer server) => ["quit"];
    public override int       GetRconPort(GameServer server)        => server.ServerPort;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override Task PreStartAsync(GameServer s)
    {
        WriteSrcdsServerCfg(s, "game/csgo");
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var map     = S(s, "map",      "de_dust2");
        var gameType = S(s, "gameType", "0");
        var gameMode = S(s, "gameMode", "0");
        return $"-dedicated -console -usercon{SrcdsIpArg(s)} -port {s.ServerPort} " +
               $"+map {map} +game_type {gameType} +game_mode {gameMode} " +
               $"+sv_setsteamaccount {s.Gslt} +maxplayers {s.MaxPlayers}{SrcdsRconArg(s)}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"]      = "de_dust2",
        ["gameType"] = "0",
        ["gameMode"] = "0",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map",      Label = "Oletuskartta",  FieldType = ConfigFieldType.Text,     DefaultValue = "de_dust2" },
            new() { Key = "gameType", Label = "Game type",     FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",
                    Options = ["0","1","2","3"] },
            new() { Key = "gameMode", Label = "Game mode",     FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",
                    Options = ["0","1","2"] },
        ]);
        return fields;
    }
}
