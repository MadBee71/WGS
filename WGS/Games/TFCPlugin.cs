using WGS.Models;

namespace WGS.Games;

public class TFCPlugin : GoldSrcPluginBase
{
    public override string GameId           => "tfc";
    public override string GameName         => "Team Fortress Classic";
    public override string Description      => "Team Fortress Classic GoldSrc dedicated server";
    public override string Category         => "FPS";
    public override int    GameStoreAppId   => 20;
    public override int    DefaultMaxPlayers => 24;
    protected override string GameDir       => "tfc";

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
