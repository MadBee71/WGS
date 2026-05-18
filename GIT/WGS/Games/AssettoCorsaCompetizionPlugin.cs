using WGS.Models;

namespace WGS.Games;

public class AssettoCorsaCompetizionPlugin : GamePluginBase
{
    public override string GameId          => "acc";
    public override string GameName        => "Assetto Corsa Competizione";
    public override string Description     => "Official GT World Challenge racing simulator";
    public override string Category        => "Racing";
    public override int    SteamAppId      => 1430110;
    public override int    GameStoreAppId  => 805550;
    public override string Executable      => @"server\accServer.exe";
    public override int    DefaultPort     => 9600;
    public override int    DefaultQueryPort => 9601;
    public override int    DefaultMaxPlayers => 24;
    public override bool   RequiresSteamLogin => true;

    public override string BuildStartArguments(GameServer s) => "";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
