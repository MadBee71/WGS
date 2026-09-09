using WGS.Models;

namespace WGS.Games;

public class ColonySurvivalPlugin : GamePluginBase
{
    public override string GameId          => "colonysurvival";
    public override string GameName        => "Colony Survival";
    public override string Description     => "Voxel-based colony building and survival game";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 748090;
    public override int    GameStoreAppId  => 366090;
    public override string Executable      => "colonyserverdedicated.exe";
    public override int    DefaultPort     => 27005;
    public override int    DefaultQueryPort => 27005;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
    {
        var world = S(s, "worldName", "world");
        var name  = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Colony Survival Server" : s.ServerName;
        return $"-batchmode -nographics +server.world \"{world}\" +server.networktype SteamOnline +server.name \"{name}\" +server.port {s.ServerPort}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["worldName"] = "world",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName", Label = "World name", FieldType = ConfigFieldType.Text, DefaultValue = "world" },
        ]);
        return fields;
    }
}
