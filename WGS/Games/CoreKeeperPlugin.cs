using WGS.Models;

namespace WGS.Games;

public class CoreKeeperPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "corekeeper";
    public override string GameName        => "Core Keeper";
    public override string Description     => "Underground exploration and crafting survival with co-op";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1963720;
    public override int    GameStoreAppId  => 1621690;
    public override int    WorkshopAppId   => 1621260;

    public string ModTargetDirectory => @"BepInEx/plugins";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => "Launch.bat";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;

    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s) =>
        $"-batchmode -nographics -port {s.ServerPort} -queryport {s.QueryPort} -maxplayers {s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
