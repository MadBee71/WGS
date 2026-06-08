using WGS.Models;

namespace WGS.Games;

public class ARMA3Plugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "arma3";
    public override string GameName        => "Arma 3";
    public override string Description     => "Military tactical shooter with massive modding community";
    public override string Category        => "Military";
    public override int    SteamAppId      => 233780;
    public override int    GameStoreAppId  => 107410;
    public override int    WorkshopAppId   => 107410;
    public override string Executable      => "arma3server_x64.exe";

    public string ModTargetDirectory => string.Empty;
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupAHelper.OnModDownloadedAsync(s, w, id);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupAHelper.OnModRemovedAsync(s, w, id);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _)      => GroupAHelper.BuildModArguments(ids);
    public override int    DefaultPort     => 2302;
    public override int    DefaultQueryPort => 2303;
    public override int    DefaultSteamPort => 2304;
    public override int    DefaultMaxPlayers => 64;
    public override bool   RequiresSteamLogin => true;

    public override string BuildStartArguments(GameServer s)
    {
        var cfg     = S(s, "configFile",  "server.cfg");
        var profile = S(s, "profileName", "MyArmaServer");
        var workshopMods = S(s, "__wgsWorkshopMods", "");
        var manualMods   = S(s, "mods", "");
        var allMods = string.Join(";", new[] { workshopMods, manualMods }.Where(m => !string.IsNullOrWhiteSpace(m)));
        var modArg  = string.IsNullOrWhiteSpace(allMods) ? "" : $" \"-mod={allMods}\"";
        return $"-port={s.ServerPort} -name={profile} -config={cfg} " +
               $"-maxMem=8192 -noSplash -enableHT -hugePages{modArg}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["configFile"]  = "server.cfg",
        ["profileName"] = "MyArmaServer",
        ["mods"]        = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "configFile",  Label = "Config file", FieldType = ConfigFieldType.Text, DefaultValue = "server.cfg" },
            new() { Key = "profileName", Label = "Profile name",    FieldType = ConfigFieldType.Text, DefaultValue = "MyArmaServer" },
            new() { Key = "mods",        Label = "Modit (-mod=)",   FieldType = ConfigFieldType.Text, DefaultValue = "" },
        ]);
        return fields;
    }
}
