using WGS.Models;

namespace WGS.Games;

public class SpaceEngineersPlugin : GamePluginBase
{
    public override string GameId          => "spaceengineers";
    public override string GameName        => "Space Engineers";
    public override string Description     => "Engineering and survival sandbox set in space and on planets";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 298740;
    public override int    GameStoreAppId  => 244850;
    public override string Executable      => @"DedicatedServer64\SpaceEngineersDedicated.exe";
    public override int    DefaultPort     => 27016;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s) => "-console";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
