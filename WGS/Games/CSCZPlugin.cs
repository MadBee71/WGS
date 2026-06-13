using WGS.Models;

namespace WGS.Games;

public class CSCZPlugin : GoldSrcPluginBase
{
    public override string GameId           => "cscz";
    public override string GameName         => "Counter-Strike: Condition Zero";
    public override string Description      => "Counter-Strike: Condition Zero dedicated server";
    public override string Category         => "FPS";
    public override int    GameStoreAppId   => 80;
    public override int    DefaultMaxPlayers => 20;
    protected override string GameDir       => "czero";

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
