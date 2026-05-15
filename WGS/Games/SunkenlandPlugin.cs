using WGS.Models;

namespace WGS.Games;

public class SunkenlandPlugin : GamePluginBase
{
    public override string GameId          => "sunkenland";
    public override string GameName        => "Sunkenland";
    public override string Description     => "Post-apocalyptic water world survival";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2667530;
    public override int    GameStoreAppId  => 2080690;
    public override string Executable      => "Sunkenland-DedicatedServer.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
        => $"-port {s.ServerPort} -queryport {s.QueryPort} " +
           $"-name \"{s.ServerName}\" -password \"{s.ServerPassword}\" " +
           $"-maxplayers {s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["worldName"] = "SunkenWorld",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName", Label = "Maailman nimi", FieldType = ConfigFieldType.Text, DefaultValue = "SunkenWorld" },
        ]);
        return fields;
    }
}
