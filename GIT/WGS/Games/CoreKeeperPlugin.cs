using WGS.Models;

namespace WGS.Games;

public class CoreKeeperPlugin : GamePluginBase
{
    public override string GameId          => "corekeeper";
    public override string GameName        => "Core Keeper";
    public override string Description     => "Underground exploration and crafting survival with co-op";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1963720;
    public override int    GameStoreAppId  => 1621690;
    public override string Executable      => "Launch.bat";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;

    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s) =>
        $"-batchmode -nographics -port {s.ServerPort} -queryport {s.QueryPort} -maxplayers {s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
