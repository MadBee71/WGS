using WGS.Models;

namespace WGS.Games;

public class XonoticPlugin : GamePluginBase
{
    public override string GameId          => "xonotic";
    public override string GameName        => "Xonotic";
    public override string Description     => "Free, open-source fast-paced arena shooter";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 0; // free, open source — not on Steam, downloaded directly from xonotic.org
    public override string Executable      => "xonotic-dedicated.exe";
    public override int    DefaultPort     => 26000;
    public override int    DefaultQueryPort => 26000;
    public override int    DefaultMaxPlayers => 16;

    // Xonotic has no versioned "latest" API (unlike the GitHub-hosted projects) — its releases are
    // infrequent, so this fixed URL is verified working as of this writing but will need a manual
    // bump whenever a new Xonotic version ships.
    public override Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
        => Task.FromResult<(string, string)?>(("0.8.6", "https://dl.xonotic.org/xonotic-0.8.6.zip"));

    public override string BuildStartArguments(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Xonotic Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        var passArg = string.IsNullOrWhiteSpace(pass) ? "" : $" +password \"{pass}\"";
        return $"-dedicated +port {s.ServerPort} +hostname \"{name}\" +maxplayers {s.MaxPlayers}{passArg}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
