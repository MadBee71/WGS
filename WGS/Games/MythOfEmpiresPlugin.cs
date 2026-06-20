using WGS.Models;

namespace WGS.Games;

public class MythOfEmpiresPlugin : GamePluginBase
{
    public override string GameId          => "mythofempires";
    public override string GameName        => "Myth of Empires";
    public override string Description     => "Open-world survival sandbox set in ancient China";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1794810;
    public override string Executable      => @"MOE\Binaries\Win64\MOEServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7779;
    public override int    DefaultMaxPlayers => 100;

    public override string BuildStartArguments(GameServer s)
        => "-game -server -DataLocalFile -NotCheckServerSteamAuth -log -LOCALLOGTIMES -PrivateServer -disable_qim -UseACE " +
           $"-MultiHome={s.ServerIp} -SessionName=\"{s.ServerName}\" -MaxPlayers={s.MaxPlayers} -Port={s.ServerPort} -ShutDownServicePort={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
