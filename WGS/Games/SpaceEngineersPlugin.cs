using WGS.Models;

namespace WGS.Games;

public class SpaceEngineersPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "spaceengineers";
    public override string GameName        => "Space Engineers";
    public override string Description     => "Engineering and survival sandbox set in space and on planets";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 298740;
    public override int    GameStoreAppId  => 244850;
    public override int    WorkshopAppId   => 244850;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"DedicatedServer64\SpaceEngineersDedicated.exe";
    public override int    DefaultPort     => 27016;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s) => "-console";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
