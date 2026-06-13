using WGS.Models;

namespace WGS.Games;

public class HL2DMPlugin : GamePluginBase
{
    public override string GameId           => "hl2dm";
    public override string GameName         => "Half-Life 2: Deathmatch";
    public override string Description      => "Half-Life 2: Deathmatch Source engine dedicated server";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 232370;
    public override int    GameStoreAppId   => 320;
    public override string Executable       => "srcds.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;
    public override bool   HasRcon          => true;

    public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "dm_lockdown");
        return $"-game hl2mp -console -usercon +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "dm_lockdown",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "dm_lockdown",
            Options = ["dm_lockdown", "dm_overwatch", "dm_runoff", "dm_steamlab", "dm_underpass", "dm_powerhouse"] });
        return fields;
    }
}
