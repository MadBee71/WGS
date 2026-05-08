using WGS.Models;

namespace WGS.Games;

public class TheForestPlugin : GamePluginBase
{
    public override string GameId        => "theforest";
    public override string GameName      => "The Forest";
    public override string Description   => "Survival horror on a cannibal island";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 556450;
    public override int    GameStoreAppId => 242760;
    public override string Executable    => "TheForestDedicatedServer.exe";
    public override int    DefaultPort   => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;
    public override bool   HasRcon       => true;

    public override string BuildStartArguments(GameServer s)
    {
        var difficulty = S(s, "difficulty", "Normal");
        return $"-batchmode -nographics serverName \"{s.ServerName}\" serverPlayers {s.MaxPlayers} " +
               $"serverPort {s.ServerPort} serverQueryPort {s.QueryPort} " +
               $"serverPassword \"{s.ServerPassword}\" difficulty {difficulty}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["difficulty"]    = "Normal",
        ["caves"]         = "true",
        ["enemies"]       = "true",
        ["vegan"]         = "false",
        ["veganEnemies"]  = "false",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "difficulty",   Label = "Vaikeustaso",   FieldType = ConfigFieldType.Dropdown, DefaultValue = "Normal", Options = ["Peaceful","Normal","Hard","HardSurvival"] },
            new() { Key = "caves",        Label = "Luolat",        FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
            new() { Key = "enemies",      Label = "Viholliset",    FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
            new() { Key = "vegan",        Label = "Vegan-tila",    FieldType = ConfigFieldType.Toggle,   DefaultValue = "false" },
        ]);
        return fields;
    }
}
