using WGS.Models;

namespace WGS.Games;

public class NightingalePlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "nightingale";
    public override string GameName        => "Nightingale";
    public override string Description     => "Victorian gaslamp fantasy co-op survival game";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3796810; // dedicated server tool — different from the game's own appid
    public override int    GameStoreAppId  => 1928980;
    public override string Executable      => "NWXServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 6;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"-log -port={s.ServerPort} -statusPort={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
