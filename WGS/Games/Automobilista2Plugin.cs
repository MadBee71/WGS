using WGS.Models;

namespace WGS.Games;

public class Automobilista2Plugin : GamePluginBase
{
    public override string GameId          => "ams2";
    public override string GameName        => "Automobilista 2";
    public override string Description     => "⚠ Requires manual server.cfg setup AFTER install (config_sample\\ folder, UserGuide.pdf) — track rotation and admin settings are not configured here";
    public override string Category        => "Racing";
    public override int    SteamAppId      => 1338040;
    public override int    GameStoreAppId  => 1066890;
    public override string Executable      => "DedicatedServerCmd.exe";
    public override int    DefaultPort     => 27016;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 26;

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new();

    // AMS2's event/track rotation, port and admin settings all live in server.cfg next to the
    // exe — the format is documented in Reiza's own UserGuide.pdf shipped with the server, with
    // sample files under config_sample\. WGS can't safely write a guessed schema here, so the
    // user configures it directly (same first-run pattern as FiveM/RedM's txAdmin).
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
