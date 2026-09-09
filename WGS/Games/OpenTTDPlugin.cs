using System.IO;
using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class OpenTTDPlugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId          => "openttd";
    public override string GameName        => "OpenTTD";
    public override string Description     => "Open-source remake of Transport Tycoon Deluxe with online multiplayer";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 0; // free, open source — downloaded directly from openttd.org
    public override string Executable      => "openttd.exe";
    public override int    DefaultPort     => 3979;
    public override int    DefaultQueryPort => 3979;
    public override int    DefaultMaxPlayers => 25;

    public override async Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
    {
        try
        {
            var json = await _http.GetStringAsync("https://api.github.com/repos/OpenTTD/OpenTTD/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString();
            if (string.IsNullOrWhiteSpace(tag)) return null;
            // Binaries are published on openttd.org's own CDN, not as GitHub release assets.
            var url = $"https://cdn.openttd.org/openttd-releases/{tag}/openttd-{tag}-windows-win64.zip";
            return (tag, url);
        }
        catch { return null; }
    }

    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "openttd.cfg"), BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS OpenTTD Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        return
            $"""
            [network]
            server_port = {s.ServerPort}
            server_name = {name}
            max_clients = {s.MaxPlayers}
            server_password = {pass}
            server_advertise = false
            """;
    }

    public override string BuildStartArguments(GameServer s) => "-D";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
