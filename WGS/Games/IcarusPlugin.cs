using WGS.Models;

namespace WGS.Games;

public class IcarusPlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "icarus";
    public override string GameName        => "Icarus";
    public override string Description     => "Session-based survival on an alien world";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2089300;
    public override int    GameStoreAppId  => 1149460;
    public override string Executable      => "IcarusServer.exe";
    public override int    DefaultPort     => 17777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 8;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"-SteamServerName=\"{s.ServerName}\" -Port={s.ServerPort} -QueryPort={s.QueryPort} -Log";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
