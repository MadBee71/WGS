using WGS.Models;

namespace WGS.Games;

public class BarotraumaPlugin : GamePluginBase
{
    public override string GameId          => "barotrauma";
    public override string GameName        => "Barotrauma";
    public override string Description     => "Co-op submarine survival horror set beneath the ice of Europa";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1026340;
    public override int    GameStoreAppId  => 602960;
    public override string Executable      => "DedicatedServer.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 16;

    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s) => $"-port {s.ServerPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
