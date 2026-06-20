using WGS.Models;

namespace WGS.Games;

public class FrozenFlamePlugin : GamePluginBase
{
    public override string GameId          => "frozenflame";
    public override string GameName        => "FrozenFlame";
    public override string Description     => "Co-op survival crafting and exploration";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1348640;
    public override string Executable      => "FrozenFlameServer.exe";
    public override int    DefaultPort      => 25575;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
        => $"-batchmode -port={s.ServerPort} -queryPort={s.QueryPort} -MetaGameServerName=\"{s.ServerName}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
