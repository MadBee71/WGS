using WGS.Models;

namespace WGS.Games;

public class AssettoCorsaCompetizionPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "acc";
    public override string GameName        => "Assetto Corsa Competizione";
    public override string Description     => "Official GT World Challenge racing simulator";
    public override string Category        => "Racing";
    public override int    SteamAppId      => 1430110;
    public override int    WorkshopAppId   => 805550;

    public string ModTargetDirectory => "mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModDownloadedAsync(s, w, id, n, "cfg/configuration.json", "Mods");
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModRemovedAsync(s, id, "cfg/configuration.json");
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override int    GameStoreAppId  => 805550;
    public override string Executable      => @"server\accServer.exe";
    public override int    DefaultPort     => 9600;
    public override int    DefaultQueryPort => 9601;
    public override int    DefaultMaxPlayers => 24;
    public override bool   RequiresSteamLogin => true;

    public override string BuildStartArguments(GameServer s) => "";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
