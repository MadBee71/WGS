using WGS.Models;

namespace WGS.Games;

public class ArkSurvivalAscendedPlugin : GamePluginBase
{
    public override string GameId          => "arksurvivalascended";
    public override string GameName        => "ARK: Survival Ascended";
    public override string Description     => "Unreal Engine 5 remaster of ARK: Survival Evolved";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2430930;
    public override int    GameStoreAppId  => 2399830;
    public override string Executable      => @"ShooterGame\Binaries\Win64\ArkAscendedServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 40;

    public override string BuildStartArguments(GameServer s)
        => $"TheIsland_WP?listen?SessionName=\"{s.ServerName}\"?Port={s.ServerPort}?QueryPort={s.QueryPort}?MaxPlayers={s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
