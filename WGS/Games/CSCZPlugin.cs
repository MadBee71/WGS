using WGS.Models;

namespace WGS.Games;

public class CSCZPlugin : GamePluginBase
{
    public override string GameId           => "cscz";
    public override string GameName         => "Counter-Strike: Condition Zero";
    public override string Description      => "Counter-Strike: Condition Zero dedicated server";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 90;
    public override int    GameStoreAppId   => 80;
    public override string Executable       => "hlds.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 20;
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
        var map = S(s, "map", "de_dust2");
        return $"-console -game czero +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers} -norestart";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "de_dust2",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "de_dust2",
            Options = ["de_dust2", "de_inferno", "de_nuke", "cs_assault", "cs_italy"] });
        return fields;
    }
}
