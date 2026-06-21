using WGS.Models;

namespace WGS.Games;

public class EcoGlobalSurvivalPlugin : GamePluginBase
{
    public override string GameId          => "ecoglobalsurvival";
    public override string GameName        => "Eco: Global Survival";
    public override string Description     => "Multiplayer survival game about building a civilization without destroying the ecosystem — most server settings (public/private, password, world name) are managed through Eco's own web admin panel, not WGS's config fields";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 739590;
    public override int    GameStoreAppId  => 382310;
    public override string Executable      => "EcoServer.exe";
    public override int    DefaultPort      => 3000;
    public override int    DefaultQueryPort => 3001;
    public override int    DefaultMaxPlayers => 20;

    public override string BuildStartArguments(GameServer s)
        => $"-userId=1 -port={s.ServerPort} -webPort={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
