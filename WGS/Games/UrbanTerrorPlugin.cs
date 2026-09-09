using System.IO;
using WGS.Models;

namespace WGS.Games;

public class UrbanTerrorPlugin : GamePluginBase
{
    public override string GameId          => "urbanterror";
    public override string Description     => "⚠ Manual install required — download and install it yourself, then point this server at that folder — free tactical FPS built on the idTech3 engine";
    public override string GameName        => "Urban Terror";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 0;
    // Not on Steam — urbanterror.info's download page routes every link (including nonexistent
    // paths) through an obfuscated redirector that always returns 200 OK, so no download URL
    // could be verified reliably. Manual install: get it from urbanterror.info/downloads yourself.
    public override string Executable      => "ioUrTded.exe";
    public override int    DefaultPort     => 27960;
    public override int    DefaultQueryPort => 27960;
    public override int    DefaultMaxPlayers => 16;

    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "q3ut4", "server.cfg"), BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Urban Terror Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        return
            $"""
            seta sv_hostname "{name}"
            seta sv_maxclients {s.MaxPlayers}
            seta g_password "{pass}"
            seta net_port {s.ServerPort}
            """;
    }

    public override string BuildStartArguments(GameServer s) => $"+seta net_port {s.ServerPort} +exec server.cfg";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
