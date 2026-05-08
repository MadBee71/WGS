using WGS.Models;

namespace WGS.Games;

public class SCUMPlugin : GamePluginBase
{
    public override string GameId          => "scum";
    public override string GameName        => "SCUM";
    public override string Description     => "Open world survival with advanced mechanics";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3792580;
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
