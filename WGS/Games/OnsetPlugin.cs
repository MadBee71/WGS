using WGS.Models;

namespace WGS.Games;

public class OnsetPlugin : GamePluginBase
{
    public override string GameId          => "onset";
    public override string GameName        => "Onset";
    public override string Description     => "GTA-inspired sandbox roleplay engine with a scriptable dedicated server";
    public override string Category        => "Open World";
    public override int    SteamAppId      => 1204170;
    public override string Executable      => "OnsetServer.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 7776;
    public override int    DefaultMaxPlayers => 32;

    public override string BuildStartArguments(GameServer s) => string.Empty;

    // Server name, ports and gamemode settings live in server_config.json next to OnsetServer.exe —
    // the full schema isn't published cleanly enough to write with confidence, so it's left for the
    // user to configure directly (same first-run pattern as FiveM/RedM's txAdmin).
    public override List<ConfigField> GetConfigFields() => BaseFields();

    public override Dictionary<string, string> GetDefaultSettings() => new();
}
