using WGS.Models;

namespace WGS.Games;

public class VoyagersOfNeraPlugin : GamePluginBase
{
    public override string GameId          => "voyagersofnera";
    public override string GameName        => "Voyagers of Nera";
    public override string Description     => "Co-op sailing and exploration survival game";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3937860;
    public override string Executable      => @"BoatGame\Binaries\Win64\BoatGameServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7778;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
        => $"-port={s.ServerPort} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
