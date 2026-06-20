using WGS.Models;

namespace WGS.Games;

public class PixArkPlugin : GamePluginBase
{
    public override string GameId          => "pixark";
    public override string GameName        => "PixARK";
    public override string Description     => "Voxel-world survival spin-off of ARK";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 824360;
    public override string Executable      => @"ShooterGame\Binaries\Win64\PixARKServer.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 10;

    public override string BuildStartArguments(GameServer s)
        => $"CubeWorld_Light?listen?SessionName=\"{s.ServerName}\"?MultiHome={s.ServerIp}?Port={s.ServerPort}?QueryPort={s.QueryPort}?MaxPlayers={s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
