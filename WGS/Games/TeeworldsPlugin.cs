using System.IO;
using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class TeeworldsPlugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId          => "teeworlds";
    public override string GameName        => "Teeworlds";
    public override string Description     => "Free, lightweight retro 2D multiplayer shooter";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 0; // free, open source — downloaded directly from GitHub releases
    public override string Executable      => "teeworlds_srv.exe";
    public override int    DefaultPort     => 8303;
    public override int    DefaultQueryPort => 8303;
    public override int    DefaultMaxPlayers => 12;

    public override async Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
    {
        try
        {
            var json = await _http.GetStringAsync("https://api.github.com/repos/teeworlds/teeworlds/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "unknown";
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains("win64", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip"))
                    return (tag, asset.GetProperty("browser_download_url").GetString()!);
            }
            return null;
        }
        catch { return null; }
    }

    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "autoexec.cfg"), BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Teeworlds Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        var rcon = S(s, "rconPassword", "");
        return
            $"""
            sv_name {name}
            sv_port {s.ServerPort}
            sv_max_clients {s.MaxPlayers}
            password {pass}
            sv_rcon_password {rcon}
            sv_register 1
            """;
    }

    public override string BuildStartArguments(GameServer s) => "-f autoexec.cfg";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["rconPassword"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "rconPassword", Label = "RCON password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
