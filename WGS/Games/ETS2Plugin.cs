using WGS.Models;

namespace WGS.Games;

public class ETS2Plugin : GamePluginBase
{
    public override string GameId        => "ets2";
    public override string GameName      => "Euro Truck Simulator 2";
    public override string Description   => "Multiplayer trucking convoy server";
    public override string Category      => "Simulation";
    public override int    SteamAppId    => 1948160;
    public override int    GameStoreAppId => 227300;
    public override string Executable    => @"bin\win_x64\eurotrucks2_server.exe";
    public override int    DefaultPort   => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
    {
        var cfg = S(s, "configFile", "server_config.sii");
        return $"-nosingle -server \"{s.InstallPath}\\{cfg}\"";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["configFile"]    = "server_config.sii",
        ["serverWelcome"] = "Welcome!",
        ["moderatorList"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "configFile",    Label = "Config-tiedosto",  FieldType = ConfigFieldType.Text, DefaultValue = "server_config.sii" },
            new() { Key = "serverWelcome", Label = "Tervetuloviesti",  FieldType = ConfigFieldType.Text, DefaultValue = "Welcome!" },
        ]);
        return fields;
    }
}
