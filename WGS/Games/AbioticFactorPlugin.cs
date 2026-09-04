using WGS.Models;

namespace WGS.Games;

public class AbioticFactorPlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "abioticfactor";
    public override string GameName        => "Abiotic Factor";
    public override string Description     => "Co-op sci-fi survival game set in a secret underground research facility";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2857200; // dedicated server tool — different from the game's own appid (427410)
    public override int    GameStoreAppId  => 427410;
    public override string Executable      => @"AbioticFactor\Binaries\Win64\AbioticFactorServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 6;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"-log -newconsole -useperfthreads -NoAsyncLoadingThread -MaxServerPlayers={s.MaxPlayers} " +
           $"-PORT={s.ServerPort} -QueryPort={s.QueryPort} -ServerPassword=\"{s.ServerPassword}\" " +
           $"-AdminPassword=\"{s.RconPassword}\" -SteamServerName=\"{s.ServerName}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
