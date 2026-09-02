using System.IO;
using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class BedrockPlugin : GamePluginBase
{
    public override string GameId          => "minecraft_bedrock";
    public override string GameName        => "Minecraft Bedrock";
    public override string Description     => "Minecraft Bedrock Edition dedicated server";
    public override string Category        => "Sandbox";
    public override int    SteamAppId      => 0;
    public override string Executable      => "bedrock_server.exe";
    public override int    DefaultPort     => 19132;
    public override int    DefaultQueryPort => 19132;
    public override int    DefaultMaxPlayers => 10;

    private static readonly HttpClient _http = new();

    // Community-maintained JSON index of all Bedrock server releases and their download URLs.
    // Much more reliable than scraping Minecraft's own download page HTML.
    private const string IndexUrl = "https://raw.githubusercontent.com/kittizz/bedrock-server-downloads/main/bedrock-server-downloads.json";

    private static async Task<(string version, string url)?> GetLatestWindowsAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(IndexUrl);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("release", out var releases)) return null;

            // Keys are version strings — last one in document order is the latest.
            string? latestVersion = null;
            string? latestUrl     = null;
            foreach (var entry in releases.EnumerateObject())
            {
                if (entry.Value.TryGetProperty("windows", out var win) &&
                    win.TryGetProperty("url", out var urlProp))
                {
                    latestVersion = entry.Name;
                    latestUrl     = urlProp.GetString();
                }
            }
            if (latestVersion == null || latestUrl == null) return null;
            return (latestVersion, latestUrl);
        }
        catch { return null; }
    }

    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        log("[Bedrock] Fetching latest Bedrock server version...");
        var latest = await GetLatestWindowsAsync();
        if (latest == null)
        {
            log("[Bedrock] Could not fetch the Bedrock server version list. Check your internet connection.");
            return false;
        }

        var (version, url) = latest.Value;
        log($"[Bedrock] Found Bedrock server {version}. Downloading...");

        var zipPath = Path.Combine(server.InstallPath, "bedrock-server.zip");
        try
        {
            using var stream = await _http.GetStreamAsync(url);
            using var file   = File.Create(zipPath);
            await stream.CopyToAsync(file);
        }
        catch (Exception ex)
        {
            log($"[Bedrock] Download failed: {ex.Message}");
            return false;
        }

        log("[Bedrock] Extracting...");
        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, server.InstallPath, overwriteFiles: true);
            File.Delete(zipPath);
        }
        catch (Exception ex)
        {
            log($"[Bedrock] Extraction failed: {ex.Message}");
            return false;
        }

        server.GameSpecificSettings["installedBuild"] = version;
        log($"[Bedrock] Bedrock server {version} installed successfully.");
        return true;
    }

    public override Task PreStartAsync(GameServer s)
    {
        var propsPath = Path.Combine(s.InstallPath, "server.properties");
        WriteConfigIfMissing(propsPath, BuildServerProperties(s));
        return Task.CompletedTask;
    }

    private string BuildServerProperties(GameServer s)
    {
        var gamemode   = S(s, "gamemode",   "survival");
        var difficulty = S(s, "difficulty", "easy");
        var onlineMode = S(s, "onlineMode", "true");

        return
            $"""
            server-name={s.ServerName}
            gamemode={gamemode}
            difficulty={difficulty}
            allow-cheats=false
            max-players={s.MaxPlayers}
            online-mode={onlineMode}
            white-list=false
            server-port={s.ServerPort}
            server-portv6={s.ServerPort + 1}
            view-distance=32
            tick-distance=4
            player-idle-timeout=30
            level-name=Bedrock level
            level-seed=
            default-player-permission-level=member
            texturepack-required=false
            """;
    }

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["gamemode"]   = "survival",
        ["difficulty"] = "easy",
        ["onlineMode"] = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "gamemode",   Label = "Game mode",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival",  Options = ["survival", "creative", "adventure"] },
            new() { Key = "difficulty", Label = "Difficulty", FieldType = ConfigFieldType.Dropdown, DefaultValue = "easy",      Options = ["peaceful", "easy", "normal", "hard"] },
            new() { Key = "onlineMode", Label = "Online mode",FieldType = ConfigFieldType.Toggle,   DefaultValue = "true",
                    Description = "Requires players to be signed in to a Microsoft account. Disable for LAN-only play." },
        ]);
        return fields;
    }
}
