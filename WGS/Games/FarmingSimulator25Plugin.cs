using WGS.Models;

namespace WGS.Games;

public class FarmingSimulator25Plugin : GamePluginBase
{
    public override string GameId          => "farmingsimulator25";
    public override string GameName        => "Farming Simulator 25";
    public override string Description     => "⚠ Requires a separate dedicated-server licence purchased directly from Giants Software BEFORE anything works — not available through Steam";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 0; // no SteamCMD path exists — the dedicated server build is a separate paid download from Giants' own site
    public override string Executable      => "dedicatedServer.exe";
    public override int    DefaultPort     => 10823;
    public override int    DefaultQueryPort => 10823;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s) => string.Empty;

    // Server name, port and passwords live in dedicatedServerConfig.xml under
    // Documents\MyGames\FarmingSimulator2025\dedicated_server — outside the install folder
    // entirely, and shared globally per Windows user account rather than per install path. WGS
    // cannot safely write this file (schema not fully published, and running more than one FS25
    // server under the same Windows account may need separate profiles to avoid clashing).
    public override List<ConfigField> GetConfigFields() => BaseFields();

    public override Dictionary<string, string> GetDefaultSettings() => new();
}
