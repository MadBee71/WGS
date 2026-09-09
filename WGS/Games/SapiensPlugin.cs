using WGS.Models;

namespace WGS.Games;

public class SapiensPlugin : GamePluginBase
{
    public override string GameId          => "sapiens";
    public override string GameName        => "Sapiens";
    public override string Description     => "Colony survival across generations";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2886350;
    public override int    GameStoreAppId  => 1060230;
    public override string Executable      => "SapiensServer.exe";
    public override int    DefaultPort     => 28015;
    public override int    DefaultQueryPort => 28016;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s) => string.Empty;

    // Configured via config.lua next to the server executable — schema not confirmed enough to
    // write automatically.
    public override List<ConfigField> GetConfigFields() => BaseFields();

    public override Dictionary<string, string> GetDefaultSettings() => new();
}
