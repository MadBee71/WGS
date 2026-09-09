using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class LuantiPlugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId          => "luanti";
    public override string GameName        => "Luanti (Minetest)";
    public override string Description     => "Open-source voxel game engine with easy modding — formerly known as Minetest";
    public override string Category        => "Sandbox";
    public override int    SteamAppId      => 0; // free, open source — downloaded directly from GitHub releases
    public override string Executable      => @"bin\luanti.exe";
    public override string? GetWorkingDirectory(GameServer server) => server.InstallPath;
    public override int    DefaultPort     => 30000;
    public override int    DefaultQueryPort => 30000;
    public override int    DefaultMaxPlayers => 16;

    // Luanti's release asset filename embeds the version number (e.g. luanti-5.17.0-win64.zip),
    // so it must be looked up per release rather than guessed as a fixed "latest/download" URL.
    public override async Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
    {
        try
        {
            var json = await _http.GetStringAsync("https://api.github.com/repos/luanti-org/luanti/releases/latest");
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

    public override string BuildStartArguments(GameServer s)
        => $"--server --port {s.ServerPort} --worldname \"{S(s, "worldName", "world")}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["worldName"] = "world",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName", Label = "World name", FieldType = ConfigFieldType.Text, DefaultValue = "world" },
        ]);
        return fields;
    }
}
