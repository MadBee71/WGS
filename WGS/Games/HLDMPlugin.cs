using WGS.Models;

namespace WGS.Games;

public class HLDMPlugin : GoldSrcPluginBase
{
    public override string GameId           => "hldm";
    public override string GameName         => "Half-Life Deathmatch";
    public override string Description      => "Half-Life Deathmatch GoldSrc dedicated server";
    public override string Category         => "FPS";
    public override int    GameStoreAppId   => 70;
    public override int    DefaultMaxPlayers => 16;
    protected override string GameDir       => "valve";

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "crossfire");
        return $"-console -game valve +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers} -norestart";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "crossfire",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "crossfire",
            Options = ["crossfire", "bounce", "datacore", "frenzy", "lambda_bunker", "undertow"] });
        return fields;
    }
}
