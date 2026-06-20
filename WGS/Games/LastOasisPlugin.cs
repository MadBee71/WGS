using WGS.Models;

namespace WGS.Games;

public class LastOasisPlugin : GamePluginBase
{
    public override string GameId          => "lastoasis";
    public override string GameName        => "Last Oasis";
    public override string Description     => "Nomadic survival MMO on walking mobile bases";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 920720;
    public override string Executable      => @"Mist\Binaries\Win64\MistServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 5555;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 100;

    public override string BuildStartArguments(GameServer s)
        => $"-log -force_steamclient_link -messaging -NoLiveServer -EnableCheats " +
           $"-port={s.ServerPort} -slots={s.MaxPlayers} -QueryPort={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
