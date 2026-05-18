using WGS.Models;

namespace WGS.Games;

public class BlackMesaPlugin : GamePluginBase
{
    public override string GameId          => "blackmesa";
    public override string GameName        => "Black Mesa";
    public override string Description     => "Source engine Black Mesa dedicated server";
    public override string Category        => "Shooter";
    public override int    SteamAppId      => 346680;
    public override int    GameStoreAppId  => 362890;
    public override string Executable      => "srcds.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 24;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s)
    {
        var map     = S(s, "mapName", "gasworks");
        var tickrate = S(s, "tickrate", "64");
        return $"-game bms -console -map {map} -maxplayers {s.MaxPlayers} -port {s.ServerPort} -tickrate {tickrate}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mapName"]  = "gasworks",
        ["tickrate"] = "64",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "mapName",  Label = "Map",      FieldType = ConfigFieldType.Text,     DefaultValue = "gasworks" },
            new() { Key = "tickrate", Label = "Tickrate",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "64", Options = ["64", "128"] },
        ]);
        return fields;
    }
}
