using WGS.Models;

namespace WGS.Games;

public class HLOpForPlugin : GoldSrcPluginBase
{
    public override string GameId           => "hlopfor";
    public override string GameName         => "Half-Life: Opposing Force";
    public override string Description      => "Half-Life: Opposing Force GoldSrc dedicated server";
    public override string Category         => "FPS";
    public override int    GameStoreAppId   => 50;
    public override int    DefaultMaxPlayers => 16;
    protected override string GameDir       => "gearbox";

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
