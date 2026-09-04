using WGS.Models;

namespace WGS.Games;

public class HumanitZPlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "humanitz";
    public override string GameName        => "HumanitZ";
    public override string Description     => "Open-world zombie survival co-op";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2728330;
    public override string Executable      => @"HumanitZServer\Binaries\Win64\HumanitZServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
