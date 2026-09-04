using WGS.Models;

namespace WGS.Games;

public class OperationHarshDoorstopPlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "operationharshdoorstop";
    public override string GameName        => "Operation: Harsh Doorstop";
    public override string Description     => "Tactical low-poly multiplayer FPS";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 950900;
    public override string Executable      => @"HarshDoorstop\Binaries\Win64\HarshDoorstopServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27005;
    public override int    DefaultMaxPlayers => 16;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"-log -port={s.ServerPort} -name=\"{s.ServerName}\" MaxPlayers={s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
