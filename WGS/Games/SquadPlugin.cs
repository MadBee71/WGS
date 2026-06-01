using WGS.Models;

namespace WGS.Games;

public class SquadPlugin : GamePluginBase
{
    public override string GameId          => "squad";
    public override string GameName        => "Squad";
    public override string Description     => "Large-scale military teamwork FPS with realistic gunplay";
    public override string Category        => "Military";
    public override int    SteamAppId      => 403240;
    public override int    GameStoreAppId  => 393380;
    public override string Executable      => "SquadServer.exe";
    public override int    DefaultPort     => 7787;
    public override int    DefaultQueryPort => 27165;
    public override int    DefaultMaxPlayers => 100;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s) =>
        $"+PORT {s.ServerPort} +QUERYPORT {s.QueryPort} +FIXEDMAXPLAYERS {s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
