using WGS.Models;

namespace WGS.Games;

public class TowerUnitePlugin : GamePluginBase
{
    public override string GameId          => "towerunite";
    public override string GameName        => "Tower Unite";
    public override string Description     => "Social multiplayer game world with minigames, condos and arcades";
    public override string Category        => "Other";
    public override int    SteamAppId      => 439660;
    public override int    GameStoreAppId  => 394690;
    public override string Executable      => "TowerServer.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 32;

    public override string BuildStartArguments(GameServer s)
        // QueryPort must differ from Steam's own client port (27015) or the server can't be
        // told apart from a running Steam client on the same machine — hence the +10 offset.
        => $"-log -Port={s.ServerPort} -QueryPort={(s.QueryPort > 0 ? s.QueryPort : s.ServerPort + 1)}";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
