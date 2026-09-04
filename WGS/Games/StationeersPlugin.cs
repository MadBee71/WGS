using WGS.Models;

namespace WGS.Games;

public class StationeersPlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "stationeers";
    public override string GameName        => "Stationeers";
    public override string Description     => "Space station building and survival simulation";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 600760;
    public override int    GameStoreAppId  => 544550;
    public override string Executable      => "rocketstation_DedicatedServer.exe";
    public override int    DefaultPort      => 27500;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -nographics -port {s.ServerPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
