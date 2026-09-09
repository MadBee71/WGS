using WGS.Models;

namespace WGS.Games;

public class BlackwakePlugin : GamePluginBase
{
    public override string GameId          => "blackwake";
    public override string GameName        => "Blackwake";
    public override string Description     => "Age-of-sail naval combat with crewed ships and boarding battles";
    public override string Category        => "Military";
    public override int    SteamAppId      => 423410;
    public override int    GameStoreAppId  => 420290;
    public override string Executable      => "BlackwakeServer.exe";
    public override int    DefaultPort     => 25001;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 54;

    public override string BuildStartArguments(GameServer s) => "-batchmode -nographics";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
