using WGS.Models;

namespace WGS.Games;

public class TFCPlugin : GamePluginBase
{
    public override string GameId           => "tfc";
    public override string GameName         => "Team Fortress Classic";
    public override string Description      => "Team Fortress Classic GoldSrc dedicated server";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 90;
    public override int    GameStoreAppId   => 20;
    public override string Executable       => "hlds.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 24;
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
        var map = S(s, "map", "2fort");
        return $"-console -game tfc +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers} -norestart";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "2fort",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "2fort",
            Options = ["2fort", "dustbowl", "avanti", "casbah", "hunted", "well"] });
        return fields;
    }
}
