using WGS.Models;

namespace WGS.Games;

public class ShatterlinePlugin : GamePluginBase
{
    public override string GameId          => "shatterline";
    public override string GameName        => "Shatterline";
    public override string Description     => "Fast-paced multiplayer FPS";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 4167970;
    public override string Executable      => @"Bin.Release.Dedicated\Shatterline_Server.exe";
    public override int    DefaultPort      => 30090;
    public override int    DefaultQueryPort => 30090;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
        => $"+sv_port {s.ServerPort} --server_config game_server_config.json";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
