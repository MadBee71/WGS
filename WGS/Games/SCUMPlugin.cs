using WGS.Models;

namespace WGS.Games;

public class SCUMPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "scum";
    public override string GameName        => "SCUM";
    public override string Description     => "Open world survival with advanced mechanics";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3792580;
    public override int    WorkshopAppId   => 3792580;

    public string ModTargetDirectory => "mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModDownloadedAsync(s, w, id, n, "SCUM/Binaries/Win64/ServerSettings.ini", "Mods");
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModRemovedAsync(s, id, "SCUM/Binaries/Win64/ServerSettings.ini");
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override int    GameStoreAppId  => 513710;
    public override string Executable      => @"SCUM\Binaries\Win64\SCUMServer.exe";
    public override int    DefaultPort     => 10000;
    public override int    DefaultQueryPort => 10002;
    public override int    DefaultMaxPlayers => 32;
    public override bool   HasRcon         => true;
    public override string BuildStartArguments(GameServer s)
        => $"-port={s.ServerPort} -QueryPort={s.QueryPort} -MaxPlayers={s.MaxPlayers} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
