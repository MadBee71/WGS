using WGS.Models;

namespace WGS.Games;

public class SevenDaysToDiePlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId        => "7daystodie";
    public override string GameName      => "7 Days to Die";
    public override string Description   => "Post-apocalyptic zombie survival and base building";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 294420;
    public override int    GameStoreAppId => 251570;
    public override int    WorkshopAppId  => 251570;
    public override string Executable    => "7DaysToDieServer.exe";
    public override int    DefaultPort   => 26900;
    public override int    DefaultQueryPort => 26901;
    public override int    DefaultMaxPlayers => 8;
    public override bool   HasRcon        => true;
    public override bool   SupportsOxide  => true;
    public override string? GetBroadcastCommand(string message) => $"say \"{message}\"";
    protected override bool FilterUnityShaderNoise => true;

    public string ModTargetDirectory => "Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;

    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -nographics -dedicated -configfile=\"{s.InstallPath}\\serverconfig.xml\"";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["gameWorld"]     = "Navezgane",
        ["gameName"]      = "My Game",
        ["difficulty"]    = "2",
        ["dayNightLength"] = "60",
        ["zombieSpeed"]   = "0",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "gameWorld",      Label = "World",           FieldType = ConfigFieldType.Dropdown, DefaultValue = "Navezgane", Options = ["Navezgane","RWG"] },
            new() { Key = "gameName",       Label = "Game name",       FieldType = ConfigFieldType.Text,     DefaultValue = "My Game" },
            new() { Key = "difficulty",     Label = "Difficulty",      FieldType = ConfigFieldType.Dropdown, DefaultValue = "2", Options = ["0 - Trivial","1 - Easy","2 - Normal","3 - Hard","4 - Insane"] },
            new() { Key = "dayNightLength", Label = "Day length",      FieldType = ConfigFieldType.Slider,   DefaultValue = "60", Min = 10, Max = 120 },
            new() { Key = "zombieSpeed",    Label = "Zombie speed",    FieldType = ConfigFieldType.Dropdown, DefaultValue = "0", Options = ["0 - Walk","1 - Jog","2 - Run","3 - Sprint"] },
        ]);
        return fields;
    }
}
