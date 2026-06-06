using WGS.Models;

namespace WGS.Games;

public class DSTPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "dst";
    public override string GameName        => "Don't Starve Together";
    public override string Description     => "Survival co-op in a dark wilderness world";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 343050;
    public override int    GameStoreAppId  => 322330;
    public override int    WorkshopAppId   => 322330;

    public string ModTargetDirectory => @"mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"bin\dontstarve_dedicated_server_nullrenderer.exe";
    public override int    DefaultPort     => 10999;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 10;
    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s)
    {
        var cluster = S(s, "cluster", "MyDediServer");
        var shard   = S(s, "shard",   "Master");
        return $"-console -cluster {cluster} -shard {shard} -port {s.ServerPort}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["cluster"] = "MyDediServer",
        ["shard"]   = "Master",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "cluster", Label = "Cluster-nimi", FieldType = ConfigFieldType.Text, DefaultValue = "MyDediServer" },
            new() { Key = "shard",   Label = "Shard",        FieldType = ConfigFieldType.Dropdown, DefaultValue = "Master",
                    Options = ["Master","Caves"] },
        ]);
        return fields;
    }
}
