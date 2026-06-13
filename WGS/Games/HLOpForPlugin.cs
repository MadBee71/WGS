using WGS.Models;

namespace WGS.Games;

public class HLOpForPlugin : GamePluginBase
{
    public override string GameId           => "hlopfor";
    public override string GameName         => "Half-Life: Opposing Force";
    public override string Description      => "Half-Life: Opposing Force GoldSrc dedicated server";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 90;
    public override int    GameStoreAppId   => 50;
    public override string Executable       => "hlds.exe";
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
        var map = S(s, "map", "op4_bootcamp");
        return $"-console -game gearbox +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers} -norestart";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "op4_bootcamp",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "op4_bootcamp",
            Options = ["op4_bootcamp", "op4_park", "op4_biodomes", "op4_datacore", "op4_disposal"] });
        return fields;
    }
}
