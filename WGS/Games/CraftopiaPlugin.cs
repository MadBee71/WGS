using WGS.Models;

namespace WGS.Games;

public class CraftopiaPlugin : GamePluginBase
{
    public override string GameId          => "craftopia";
    public override string GameName        => "Craftopia";
    public override string Description     => "Open-world multiplayer survival/crafting sandbox — needs an unusually wide port range opened";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1670340; // dedicated server — different from the game's own appid (1307550)
    public override int    GameStoreAppId  => 1307550;
    public override string Executable      => "Craftopia.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -nographics -port={s.ServerPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
