using WGS.Models;

namespace WGS.Games;

public class SunkenlandPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "sunkenland";
    public override string GameName        => "Sunkenland";
    public override string Description     => "Post-apocalyptic water world survival";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2667530;
    public override int    GameStoreAppId  => 2080690;
    public override int    WorkshopAppId   => 2667530;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => "Sunkenland-DedicatedServer.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;
    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -nographics " +
           $"-port {s.ServerPort} -queryport {s.QueryPort} " +
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
