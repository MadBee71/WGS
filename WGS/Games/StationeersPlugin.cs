using WGS.Models;

namespace WGS.Games;

public class StationeersPlugin : GamePluginBase
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

    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -nographics -port {s.ServerPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
