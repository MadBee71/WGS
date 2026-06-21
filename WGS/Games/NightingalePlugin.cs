using WGS.Models;

namespace WGS.Games;

public class NightingalePlugin : GamePluginBase
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

    public override string BuildStartArguments(GameServer s)
        => $"-log -port={s.ServerPort} -statusPort={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
