using WGS.Models;

namespace WGS.Games;

public class NoOneSurvivedPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "noonessurvived";
    public override string GameName        => "No One Survived";
    public override string Description     => "Zombie survival with base building";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2329680;
    public override int    GameStoreAppId  => 1963180;
    public override int    WorkshopAppId   => 2329680;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"WRSH\Binaries\Win64\WRSHServer.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 28015;
    public override int    DefaultMaxPlayers => 50;
    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} " +
           $"-MaxPlayers={s.MaxPlayers} -ServerName=\"{s.ServerName}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
