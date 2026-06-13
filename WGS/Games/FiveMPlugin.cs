using WGS.Models;

namespace WGS.Games;

public class FiveMPlugin : GamePluginBase
{
    public override string GameId           => "fivem";
    public override string GameName         => "Grand Theft Auto V (FiveM)";
    public override string Description      => "FiveM multiplayer modification framework for GTA V";
    public override string Category         => "Open World";
    public override int    SteamAppId       => 0; // not on Steam — manual install
    public override string Executable       => "FXServer.exe";
    public override int    DefaultPort      => 30120;
    public override int    DefaultQueryPort => 30120;
    public override int    DefaultMaxPlayers => 32;
    public override bool   HasRcon          => false;

    public override string BuildStartArguments(GameServer s)
    {
        var txAdminPort = S(s, "txAdminPort", "40120");
        return $"+set sv_maxclients {s.MaxPlayers} +set endpoint_add_tcp 0.0.0.0:{s.ServerPort} +set endpoint_add_udp 0.0.0.0:{s.ServerPort} +set txAdminPort {txAdminPort}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["txAdminPort"] = "40120",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "txAdminPort", Label = "txAdmin Port", FieldType = ConfigFieldType.Number, DefaultValue = "40120", Min = 1024, Max = 65535 });
        return fields;
    }
}
