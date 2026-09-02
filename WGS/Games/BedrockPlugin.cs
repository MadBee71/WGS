using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
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

    // Minecraft's download page lists the Windows Bedrock server URL directly in the HTML.
    // The URL format is stable: https://minecraft.azureedge.net/bin-win/bedrock-server-X.Y.Z.W.zip
    private const string DownloadPageUrl = "https://www.minecraft.net/en-us/download/server/bedrock";
    private static readonly Regex _urlPattern = new(
        @"https://minecraft\.azureedge\.net/bin-win/bedrock-server-([\d.]+)\.zip",
        RegexOptions.Compiled);

    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        log("[Bedrock] Fetching Minecraft Bedrock server download page...");
        string page;
        try
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
            page = await _http.GetStringAsync(DownloadPageUrl);
        }
        catch (Exception ex)
        {
            log($"[Bedrock] Failed to reach Minecraft download page: {ex.Message}");
            return false;
        }

        var match = _urlPattern.Match(page);
        if (!match.Success)
        {
            log("[Bedrock] Could not find the Bedrock server download URL on Minecraft's website. The page layout may have changed.");
            return false;
        }

        var version = match.Groups[1].Value;
        var url     = match.Value;
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
