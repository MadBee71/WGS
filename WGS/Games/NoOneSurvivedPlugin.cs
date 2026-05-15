using WGS.Models;

namespace WGS.Games;

public class NoOneSurvivedPlugin : GamePluginBase
{
    public override string GameId          => "noonesuvived";
    public override string GameName        => "No One Survived";
    public override string Description     => "Zombie survival with base building";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2329680;
    public override int    GameStoreAppId  => 1963180;
    public override string Executable      => @"WRSH\Binaries\Win64\WRSHServer.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 28015;
    public override int    DefaultMaxPlayers => 50;

    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} " +
           $"-MaxPlayers={s.MaxPlayers} -ServerName=\"{s.ServerName}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
