using WGS.Models;

namespace WGS.Games;

public class InsurgencySandstormPlugin : GamePluginBase
{
    public override string GameId          => "sandstorm";
    public override string GameName        => "Insurgency: Sandstorm";
    public override string Description     => "Tactical close-quarters FPS with cooperative and PvP modes";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 581330;
    public override int    GameStoreAppId  => 581320;
    public override string Executable      => "InsurgencyServer.exe";
    public override int    DefaultPort     => 27102;
    public override int    DefaultQueryPort => 27131;
    public override int    DefaultMaxPlayers => 28;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s)
    {
        var map      = S(s, "map",      "Farmhouse");
        var scenario = S(s, "scenario", "Scenario_Farmhouse_Checkpoint_Security");
        return $"{map}?Scenario={scenario} -Port={s.ServerPort} -QueryPort={s.QueryPort} -MaxPlayers={s.MaxPlayers} -log";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"]      = "Farmhouse",
        ["scenario"] = "Scenario_Farmhouse_Checkpoint_Security",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map",      Label = "Map",      FieldType = ConfigFieldType.Text, DefaultValue = "Farmhouse" },
            new() { Key = "scenario", Label = "Scenario", FieldType = ConfigFieldType.Text, DefaultValue = "Scenario_Farmhouse_Checkpoint_Security" },
        ]);
        return fields;
    }
}
