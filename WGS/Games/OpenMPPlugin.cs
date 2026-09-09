using System.IO;
using WGS.Models;

namespace WGS.Games;

public class OpenMPPlugin : GamePluginBase
{
    public override string GameId          => "openmp";
    public override string GameName        => "open.mp";
    public override string Description     => "Actively maintained successor to SA-MP — GTA: San Andreas multiplayer roleplay/freeroam";
    public override string Category        => "Open World";
    public override int    SteamAppId      => 0; // free, open source — downloaded directly from GitHub releases
    public override string Executable      => "omp-server.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 7777;
    public override int    DefaultMaxPlayers => 50;

    public override Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
        => Task.FromResult<(string, string)?>(("latest", "https://github.com/openmultiplayer/open.mp/releases/latest/download/open.mp-win-x86.zip"));

    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "config.json"), BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS open.mp Server" : s.ServerName;
        var rcon = S(s, "rconPassword", "");
        return
            $$"""
            {
              "network": {
                "port": {{s.ServerPort}}
              },
              "name": "{{name}}",
              "max_players": {{s.MaxPlayers}},
              "rcon": {
                "password": "{{rcon}}",
                "enable": true
              }
            }
            """;
    }

    public override string? ValidateBeforeStart(GameServer server)
    {
        var pass = S(server, "rconPassword", "");
        return string.IsNullOrWhiteSpace(pass) || pass == "changeme"
            ? "open.mp refuses to start with an empty or default RCON password. Set one in Settings → RCON password."
            : null;
    }

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["rconPassword"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "rconPassword", Label = "RCON password", FieldType = ConfigFieldType.Password, DefaultValue = "",
                    Description = "Required — open.mp will not start with an empty or default password." },
        ]);
        return fields;
    }
}
