using WGS.Models;

namespace WGS.Games;

public class ReturnToMoriaPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "returntomoria";
    public override string GameName        => "Return to Moria";
    public override string Description     => "Co-op survival set in the mines of Moria";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3349480;
    public override int    GameStoreAppId  => 2933130;
    public override int    WorkshopAppId   => 3349480;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"Moria\Binaries\Win64\MoriaServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 20151;
    public override int    DefaultQueryPort => 20151;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} -MaxPlayers={s.MaxPlayers} " +
           $"-ServerName=\"{s.ServerName}\" -ServerPassword=\"{s.ServerPassword}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
