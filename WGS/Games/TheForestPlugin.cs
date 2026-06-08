using WGS.Models;

namespace WGS.Games;

public class TheForestPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId        => "theforest";
    public override string GameName      => "The Forest";
    public override string Description   => "Survival horror on a cannibal island";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 556450;
    public override int    GameStoreAppId => 242760;
    public override int    WorkshopAppId   => 556450;

    public string ModTargetDirectory => "Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable    => "TheForestDedicatedServer.exe";
    public override int    DefaultPort      => 27017;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 64;
    public override bool   HasRcon            => true;
    public override bool   RequiresSteamLogin => true;
    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s)
    {
        var difficulty = S(s, "difficulty", "Normal");
        var caves      = S(s, "caves",    "true")  == "true" ? "1" : "0";
        var enemies    = S(s, "enemies",  "true")  == "true" ? "1" : "0";
        var vegan      = S(s, "vegan",    "false") == "true" ? "1" : "0";
        return $"-batchmode -nographics serverName \"{s.ServerName}\" serverPlayers {s.MaxPlayers} " +
               $"serverPort {s.ServerPort} serverQueryPort {s.QueryPort} " +
               $"serverPassword \"{s.ServerPassword}\" difficulty {difficulty} " +
               $"serverCaves {caves} serverEnemies {enemies} serverVegan {vegan}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["difficulty"] = "Normal",
        ["caves"]      = "true",
        ["enemies"]    = "true",
        ["vegan"]      = "false",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "difficulty",   Label = "Vaikeustaso",   FieldType = ConfigFieldType.Dropdown, DefaultValue = "Normal", Options = ["Peaceful","Normal","Hard","HardSurvival"] },
            new() { Key = "caves",        Label = "Luolat",        FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
            new() { Key = "enemies",      Label = "Viholliset",    FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
            new() { Key = "vegan",        Label = "Vegan mode",    FieldType = ConfigFieldType.Toggle,   DefaultValue = "false" },
        ]);
        return fields;
    }
}
