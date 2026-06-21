using WGS.Models;

namespace WGS.Games;

public class GroundBranchPlugin : GamePluginBase
{
    public override string GameId          => "groundbranch";
    public override string GameName        => "GROUND BRANCH";
    public override string Description     => "Tactical multiplayer FPS — serverconfig folder is generated on first launch";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 476400; // dedicated server build — different from the game's own appid
    public override int    GameStoreAppId  => 16900;
    public override string Executable      => "GroundBranchServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 10;

    public override string BuildStartArguments(GameServer s)
        => $"-log -port={s.ServerPort} -queryport={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
