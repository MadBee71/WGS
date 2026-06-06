using WGS.Models;

namespace WGS.Games;

public class STNPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId        => "stn";
    public override string GameName      => "Survive the Nights";
    public override string Description   => "Co-op zombie survival, fortification and scavenging";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 1502300;
    public override int    GameStoreAppId => 541300;
    public override int    WorkshopAppId   => 1502300;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable    => "STNServer.exe";
    public override int    DefaultPort   => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;
    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -nographics -Port={s.ServerPort} -MaxPlayers={s.MaxPlayers} -ServerName=\"{s.ServerName}\" -Password=\"{s.ServerPassword}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["dayLength"]  = "60",
        ["nightMult"]  = "2",
        ["pvp"]        = "false",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "dayLength", Label = "Day length (min)", FieldType = ConfigFieldType.Slider,  DefaultValue = "60",    Min = 10, Max = 120 },
            new() { Key = "nightMult", Label = "Night multiplier", FieldType = ConfigFieldType.Slider,  DefaultValue = "2",     Min = 1,  Max = 5 },
            new() { Key = "pvp",       Label = "PvP enabled",     FieldType = ConfigFieldType.Toggle,  DefaultValue = "false" },
        ]);
        return fields;
    }
}
