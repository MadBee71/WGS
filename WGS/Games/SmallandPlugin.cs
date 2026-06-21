using WGS.Models;

namespace WGS.Games;

public class SmallandPlugin : GamePluginBase
{
    public override string GameId          => "smalland";
    public override string GameName        => "Smalland: Survive the Wilds";
    public override string Description     => "Co-op survival game where players are shrunk to insect size";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 808040; // dedicated server — different from the game's own appid (768200)
    public override int    GameStoreAppId  => 768200;
    public override string Executable      => @"SMALLAND\Binaries\Win64\SMALLANDServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7778;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
        => $"-log -port={s.ServerPort} -QueryPort={s.QueryPort} -MaxPlayers={s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
