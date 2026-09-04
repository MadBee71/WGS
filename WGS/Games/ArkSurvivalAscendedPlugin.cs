using WGS.Models;

namespace WGS.Games;

public class ArkSurvivalAscendedPlugin : GamePluginBase, IWipePlugin, IA2SQueryPlugin
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

    // IWipePlugin
    public IEnumerable<string> GetMapWipePaths(GameServer server)
    {
        var map = server.GameSpecificSettings.TryGetValue("mapName", out var m) ? m : "TheIsland_WP";
        return [$@"ShooterGame\Saved\SavedArks\{map}.ark",
                $@"ShooterGame\Saved\SavedArks\*.arktribe",
                $@"ShooterGame\Saved\SavedArks\*.arkprofile"];
    }

    public IEnumerable<string> GetFullWipePaths(GameServer server) => GetMapWipePaths(server);


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "mapName", "TheIsland_WP");
        return $"{map}?listen?SessionName=\"{s.ServerName}\"?Port={s.ServerPort}?QueryPort={s.QueryPort}?MaxPlayers={s.MaxPlayers}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mapName"] = "TheIsland_WP",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "mapName", Label = "Map", FieldType = ConfigFieldType.Dropdown,
            DefaultValue = "TheIsland_WP",
            Options = ["TheIsland_WP", "ScorchedEarth_WP", "Aberration_WP", "Extinction_WP", "Genesis_WP", "Gen2_WP", "Svartalfheim_WP"] });
        return fields;
    }
}
