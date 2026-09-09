using System.IO;
using WGS.Models;

namespace WGS.Games;

public class MTASAPlugin : GamePluginBase
{
    public override string GameId          => "mtasa";
    public override string GameName        => "Multi Theft Auto: San Andreas";
    public override string Description     => "⚠ Manual install required — download and install it yourself, then point this server at that folder — long-running GTA: San Andreas multiplayer modification";
    public override string Category        => "Open World";
    public override int    SteamAppId      => 0;
    // MTA:SA's official distribution is an NSIS installer .exe, not a zip — WGS can't safely
    // automate an unknown installer's silent-install flags, so this is a manual install like
    // FiveM/RedM originally were: the user installs it once, then points this server at that path.
    public override string Executable      => "MTA Server.exe";
    public override int    DefaultPort     => 22003;
    public override int    DefaultQueryPort => 22003;
    public override int    DefaultMaxPlayers => 32;

    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "mods", "deathmatch", "mtaserver.conf"), BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS MTA Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        var httpPort = s.ServerPort + 2;
        return
            $"""
            <config>
                <servername>{name}</servername>
                <serverport>{s.ServerPort}</serverport>
                <maxplayers>{s.MaxPlayers}</maxplayers>
                <httpport>{httpPort}</httpport>
                <password>{pass}</password>
            </config>
            """;
    }

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
