using WGS.Models;

namespace WGS.Games;

public class GarrysModPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId          => "garrysmod";
    public override string GameName        => "Garry's Mod";
    public override string Description     => "Sandbox physics game with endless gamemodes";
    public override string Category        => "Other";
    public override int    SteamAppId      => 4020;
    public override int    GameStoreAppId  => 4000;
    public override int    WorkshopAppId   => 4000;

    public string ModTargetDirectory => @"garrysmod\addons";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => "srcds.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 24;
    public override bool   HasRcon         => true;
    public override bool   SupportsSourceMod  => true;
    public override string SourceModGameDir => "garrysmod";

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
        WriteSrcdsServerCfg(s, "garrysmod");
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var gamemode = S(s, "gamemode", "sandbox");
        var map      = S(s, "map",      "gm_flatgrass");
        return $"-game garrysmod -console -usercon{SrcdsIpArg(s)} -port {s.ServerPort} " +
               $"+maxplayers {s.MaxPlayers} +map {map} +gamemode {gamemode} " +
               $"+hostname \"{s.ServerName}\" +sv_password \"{s.ServerPassword}\"{SrcdsRconArg(s)}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["gamemode"] = "sandbox",
        ["map"]      = "gm_flatgrass",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "gamemode", Label = "Gamemode",   FieldType = ConfigFieldType.Text, DefaultValue = "sandbox" },
            new() { Key = "map",      Label = "Oletuskartta", FieldType = ConfigFieldType.Text, DefaultValue = "gm_flatgrass" },
        ]);
        return fields;
    }
}
