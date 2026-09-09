using WGS.Models;

namespace WGS.Games;

public class HurtworldPlugin : GamePluginBase
{
    public override string GameId          => "hurtworld";
    public override string GameName        => "Hurtworld";
    public override string Description     => "Open-world survival shooter with base building";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 405100;
    public override int    GameStoreAppId  => 393420;
    public override string Executable      => "Hurtworld.exe";
    public override int    DefaultPort     => 12871;
    public override int    DefaultQueryPort => 12881;
    public override int    DefaultMaxPlayers => 20;

    public override string BuildStartArguments(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Hurtworld Server" : s.ServerName;
        return $"-batchmode -nographics -exec \"host {s.ServerPort};queryport {s.QueryPort};servername {name};maxplayers {s.MaxPlayers}\" -logfile \"gamelog.txt\"";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
