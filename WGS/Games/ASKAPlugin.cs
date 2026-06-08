using WGS.Models;

namespace WGS.Games;

public class ASKAPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "aska";
    public override string GameName        => "ASKA";
    public override string Description     => "Viking village survival co-op";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3246670;
    public override int    GameStoreAppId  => 1898300;
    public override int    WorkshopAppId   => 3246670;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => "AskaServer.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 4;
    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -nographics " +
           $"-port {s.ServerPort} -queryport {s.QueryPort} " +
           $"-name \"{s.ServerName}\" -password \"{s.ServerPassword}\" " +
           $"-maxplayers {s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["worldName"] = "ASKAWorld",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName", Label = "World name", FieldType = ConfigFieldType.Text, DefaultValue = "ASKAWorld" },
        ]);
        return fields;
    }
}
