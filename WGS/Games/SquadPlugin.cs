using WGS.Models;

namespace WGS.Games;

public class SquadPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "squad";
    public override string GameName        => "Squad";
    public override string Description     => "Large-scale military teamwork FPS with realistic gunplay";
    public override string Category        => "Military";
    public override int    SteamAppId      => 403240;
    public override int    WorkshopAppId   => 393380;

    public string ModTargetDirectory => "mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModDownloadedAsync(s, w, id, n, "SquadGame/ServerConfig/Mods.cfg", "ModList");
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModRemovedAsync(s, id, "SquadGame/ServerConfig/Mods.cfg");
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override int    GameStoreAppId  => 393380;
    public override string Executable      => "SquadServer.exe";
    public override int    DefaultPort     => 7787;
    public override int    DefaultQueryPort => 27165;
    public override int    DefaultMaxPlayers => 100;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s) =>
        $"+PORT {s.ServerPort} +QUERYPORT {s.QueryPort} +FIXEDMAXPLAYERS {s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
