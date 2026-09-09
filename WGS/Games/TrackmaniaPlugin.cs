using System.IO;
using WGS.Models;

namespace WGS.Games;

public class TrackmaniaPlugin : GamePluginBase
{
    public override string GameId          => "trackmania";
    public override string GameName        => "Trackmania";
    public override string Description     => "⚠ Requires a free Nadeo dedicated-server account BEFORE starting (Settings, create at trackmania.com — not a regular Ubisoft account) — stunt racing with a dedicated server";
    public override string Category        => "Racing";
    public override int    SteamAppId      => 0;
    // TrackmaniaServer.exe is downloaded manually from Trackmania's own dedicated-server page, and
    // login uses a dedicated-server account (created at trackmania.com, NOT a normal player
    // account) whose credentials go into dedicated_cfg.txt — nothing here can be automated by WGS.
    public override string Executable      => "TrackmaniaServer.exe";
    public override int    DefaultPort     => 2350;
    public override int    DefaultQueryPort => 5000;
    public override int    DefaultMaxPlayers => 32;

    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "UserData", "Config", "dedicated_cfg.txt"), BuildDedicatedConfig(s));
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "UserData", "Maps", "MatchSettings", "tracklist.txt"), "");
        return Task.CompletedTask;
    }

    private string BuildDedicatedConfig(GameServer s)
    {
        var login = S(s, "nadeoLogin", "");
        var pass  = S(s, "nadeoPassword", "");
        var name  = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Trackmania Server" : s.ServerName;
        var srvPass = s.ServerPassword ?? "";
        return
            $"""
            <dedicated>
              <authorization_levels>
                <level>
                  <name>SuperAdmin</name>
                  <password>{pass}</password>
                </level>
              </authorization_levels>
              <masterserver_account>
                <login>{login}</login>
                <password>{pass}</password>
              </masterserver_account>
              <server_options>
                <name>{name}</name>
                <password>{srvPass}</password>
                <max_players>{s.MaxPlayers}</max_players>
              </server_options>
            </dedicated>
            """;
    }

    public override string? ValidateBeforeStart(GameServer server)
        => string.IsNullOrWhiteSpace(S(server, "nadeoLogin", ""))
            ? "Trackmania needs a free dedicated-server account (not a regular Ubisoft account) — create one at trackmania.com and enter its login in Settings."
            : null;

    public override string BuildStartArguments(GameServer s)
        => $"/title=Trackmania /game_settings=MatchSettings/tracklist.txt /dedicated_cfg=dedicated_cfg.txt /port={s.ServerPort} /xmlrpc_port={s.QueryPort}";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["nadeoLogin"]    = "",
        ["nadeoPassword"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "nadeoLogin", Label = "Dedicated server login", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "From a dedicated-server account created at trackmania.com — not your normal player login." },
            new() { Key = "nadeoPassword", Label = "Dedicated server password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
