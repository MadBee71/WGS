using WGS.Models;

namespace WGS.Games;

public class ArmaReforgerPlugin : GamePluginBase
{
    public override string GameId        => "armareforger";
    public override string GameName      => "Arma Reforger";
    public override string Description   => "Military sandbox with mod support via Bohemia Workshop";
    public override string Category      => "Military";
    public override int    SteamAppId    => 1874900;
    public override int    GameStoreAppId => 1874880;
    public override string Executable    => "ArmaReforgerServer.exe";
    public override int    DefaultPort      => 2302;
    public override int    DefaultQueryPort => 7777;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
    {
        var cfg = S(s, "configFile", "serverConfig.json");
        return $"-config \"{s.InstallPath}\\{cfg}\" -bindPort {s.ServerPort} -a2sPort {s.QueryPort} -maxFPS 60";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["configFile"] = "serverConfig.json",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "configFile", Label = "Config JSON", FieldType = ConfigFieldType.Text, DefaultValue = "serverConfig.json" },
        ]);
        return fields;
    }
}
