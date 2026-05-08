using WGS.Models;

namespace WGS.Games;

public class SonsOfTheForestPlugin : GamePluginBase
{
    public override string GameId        => "sonsoftheforest";
    public override string GameName      => "Sons of the Forest";
    public override string Description   => "Survival horror sequel on a cannibal island";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 2465200;
    public override int    GameStoreAppId => 1326470;
    public override string Executable    => "SonsOfTheForestDS.exe";
    public override int    DefaultPort   => 8766;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;
    public override bool   HasRcon       => true;

    public override string BuildStartArguments(GameServer s)
        => string.Empty; // config via dedicatedserver.cfg

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["IpAddress"]            = "0.0.0.0",
        ["GamePort"]             = "8766",
        ["QueryPort"]            = "27016",
        ["BlobSyncPort"]         = "9700",
        ["MaxPlayers"]           = "8",
        ["GameDifficulty"]       = "Normal",
        ["EnemyDifficulty"]      = "Normal",
        ["ServerPassword"]       = "",
        ["SkipIntro"]            = "true",
        ["RegenerationEnabled"]  = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "GameDifficulty",  Label = "Pelitaso",   FieldType = ConfigFieldType.Dropdown, DefaultValue = "Normal", Options = ["Peaceful","Normal","Hard","HardSurvival"] },
            new() { Key = "EnemyDifficulty", Label = "Vihollistaso",FieldType = ConfigFieldType.Dropdown, DefaultValue = "Normal", Options = ["Peaceful","Normal","Hard","HardSurvival"] },
            new() { Key = "SkipIntro",       Label = "Ohita intro", FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
            new() { Key = "RegenerationEnabled", Label = "Regeneraatio", FieldType = ConfigFieldType.Toggle, DefaultValue = "true" },
        ]);
        return fields;
    }
}
