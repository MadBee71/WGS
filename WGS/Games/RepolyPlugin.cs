using WGS.Models;

namespace WGS.Games;

public class RepolyPlugin : GamePluginBase
{
    public override string GameId          => "repoly";
    public override string GameName        => "RePoly";
    public override string Description     => "Low-poly multiplayer survival sandbox";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1189160;
    public override string Executable      => @"RePoly\Binaries\Win64\RePolyServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
        => $"AspiraWorld?listen?Port={s.ServerPort}?QueryPort={s.QueryPort}?ip={s.ServerIp} -server -log";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
