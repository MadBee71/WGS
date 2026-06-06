using WGS.Models;

namespace WGS.Games;

public class ARMA2Plugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "arma2";
    public override string GameName        => "Arma 2: Operation Arrowhead";
    public override string Description     => "Military tactical shooter with large-scale co-op and PvP";
    public override string Category        => "Military";
    public override int    SteamAppId      => 33905;  // Arma 2: OA Dedicated Server (free)
    public override int    GameStoreAppId  => 33910;
    public override int    WorkshopAppId   => 33905;
    public override string Executable      => "Arma2OAServer.exe";
    public override int    DefaultPort     => 2302;
    public override int    DefaultQueryPort => 2303;
    public override int    DefaultMaxPlayers => 64;
    public override bool   RequiresSteamLogin => true;

    public string ModTargetDirectory => string.Empty;
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupAHelper.OnModDownloadedAsync(s, w, id);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupAHelper.OnModRemovedAsync(s, w, id);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _)      => GroupAHelper.BuildModArguments(ids);

    public override string BuildStartArguments(GameServer s)
    {
        var cfg          = S(s, "configFile",  "server.cfg");
        var profile      = S(s, "profileName", "MyArma2Server");
        var workshopMods = S(s, "__wgsWorkshopMods", "");
        var manualMods   = S(s, "mods", "");
        var allMods      = string.Join(";", new[] { workshopMods, manualMods }.Where(m => !string.IsNullOrWhiteSpace(m)));
        var modArg       = string.IsNullOrWhiteSpace(allMods) ? "" : $" \"-mod={allMods}\"";
        return $"-port={s.ServerPort} -name={profile} -config={cfg} -noSplash -noPause{modArg}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["configFile"]  = "server.cfg",
        ["profileName"] = "MyArma2Server",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "configFile",  Label = "Config-tiedosto", FieldType = ConfigFieldType.Text, DefaultValue = "server.cfg" },
            new() { Key = "profileName", Label = "Profiilinimi",    FieldType = ConfigFieldType.Text, DefaultValue = "MyArma2Server" },
        ]);
        return fields;
    }
}
