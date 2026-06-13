using WGS.Models;

namespace WGS.Games;

public class CS16Plugin : GoldSrcPluginBase
{
    public override string GameId           => "cs16";
    public override string GameName         => "Counter-Strike 1.6";
    public override string Description      => "Classic Counter-Strike 1.6 GoldSrc server";
    public override string Category         => "FPS";
    public override int    GameStoreAppId   => 10;
    public override int    DefaultMaxPlayers => 20;
    protected override string GameDir       => "cstrike";

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "de_dust2");
        return $"-console -game cstrike +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers} -norestart";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "de_dust2",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "de_dust2",
            Options = ["de_dust2", "de_inferno", "de_nuke", "cs_assault", "cs_italy", "de_aztec"] });
        return fields;
    }
}
