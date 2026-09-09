using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class VelorenPlugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId          => "veloren";
    public override string GameName        => "Veloren";
    public override string Description     => "Open-source voxel RPG inspired by Cube World and Dwarf Fortress";
    public override string Category        => "Sandbox";
    public override int    SteamAppId      => 0; // free, open source — downloaded directly from GitLab CI release artifacts
    public override string Executable      => "veloren-server-cli.exe";
    public override int    DefaultPort     => 14004;
    public override int    DefaultQueryPort => 14004;
    public override int    DefaultMaxPlayers => 16;

    // Veloren's own recommended distribution channel is the Airshipper launcher (a GUI updater),
    // which has no "just give me a server zip" mode. This instead pulls the Windows build
    // artifact GitLab attaches to each tagged release — verified present on the current release
    // (v0.18.0) via the GitLab releases API, listed there as a CI job artifact download.
    public override async Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
    {
        try
        {
            var json = await _http.GetStringAsync("https://gitlab.com/api/v4/projects/10174980/releases/permalink/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "unknown";
            foreach (var link in doc.RootElement.GetProperty("assets").GetProperty("links").EnumerateArray())
            {
                var name = link.GetProperty("name").GetString() ?? "";
                if (name.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                    return (tag, link.GetProperty("direct_asset_url").GetString()!);
            }
            return null;
        }
        catch { return null; }
    }

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
