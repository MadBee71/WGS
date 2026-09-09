using WGS.Models;

namespace WGS.Games;

public class SoldatPlugin : GamePluginBase
{
    public override string GameId          => "soldat";
    public override string GameName        => "Soldat";
    public override string Description     => "Classic 2D run-and-gun multiplayer shooter";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 638500;
    public override int    GameStoreAppId  => 298900;
    public override string Executable      => "Soldat.exe";
    public override int    DefaultPort     => 23073;
    public override int    DefaultQueryPort => 23073;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s) => "-dedicated -start";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
