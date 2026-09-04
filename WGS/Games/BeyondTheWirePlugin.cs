using WGS.Models;

namespace WGS.Games;

public class BeyondTheWirePlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "beyondthewire";
    public override string GameName        => "Beyond The Wire";
    public override string Description     => "WWI-era multiplayer FPS";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 1064780;
    public override int    GameStoreAppId  => 528480;
    public override string Executable      => @"WireGame\Binaries\Win64\WireGameServer.exe";
    public override int    DefaultPort      => 7887;
    public override int    DefaultQueryPort => 27165;
    public override int    DefaultMaxPlayers => 100;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"Port={s.ServerPort} QueryPort={s.QueryPort} -log -fullcrashdump";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
