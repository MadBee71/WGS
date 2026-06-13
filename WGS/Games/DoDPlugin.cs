using WGS.Models;

namespace WGS.Games;

public class DoDPlugin : GoldSrcPluginBase
{
    public override string GameId           => "dod";
    public override string GameName         => "Day of Defeat";
    public override string Description      => "Day of Defeat WWII GoldSrc dedicated server";
    public override string Category         => "FPS";
    public override int    GameStoreAppId   => 30;
    public override int    DefaultMaxPlayers => 20;
    protected override string GameDir       => "dod";

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "dod_anzio");
        return $"-console -game dod +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers} -norestart";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "dod_anzio",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "dod_anzio",
            Options = ["dod_anzio", "dod_avalanche", "dod_donner", "dod_flash", "dod_jagd", "dod_kalt"] });
        return fields;
    }
}
