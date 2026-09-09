using System.IO;
using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class MindustryPlugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId          => "mindustry";
    public override string GameName        => "Mindustry";
    public override string Description     => "Open-source tower-defense/factory automation game with a dedicated server jar";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 0; // free, open source — server jar downloaded from GitHub releases
    public override string Executable      => "java";
    public override int    DefaultPort     => 6567;
    public override int    DefaultQueryPort => 6567;
    public override int    DefaultMaxPlayers => 32;

    // Mindustry's server jar is a single runnable file, not a zip — GetManualDownloadInfoAsync
    // always unzips its download, which would explode this jar into its class files instead of
    // leaving a runnable server.jar. A plain file download via TryCustomInstallAsync is used instead.
    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        try
        {
            var json = await _http.GetStringAsync("https://api.github.com/repos/Anuken/Mindustry/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "unknown";
            string? url = null;
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == "server-release.jar")
                { url = asset.GetProperty("browser_download_url").GetString(); break; }
            }
            if (url == null) { log("[Mindustry] Couldn't find server-release.jar in the latest GitHub release."); return false; }

            log($"[Mindustry] Downloading {url} ...");
            using var stream = await _http.GetStreamAsync(url);
            using var file = File.Create(Path.Combine(server.InstallPath, "server.jar"));
            await stream.CopyToAsync(file);

            server.GameSpecificSettings["installedBuild"] = tag;
            return true;
        }
        catch (Exception ex) { log($"[Mindustry] Install failed: {ex.Message}"); return false; }
    }

    // Mindustry reads port/startCommands from config/config.json (relative to the jar) using the
    // exact same key names as its interactive "config <key> <value>" console command — written
    // proactively here so the server opens to the network automatically on first launch instead
    // of needing someone to type "host" by hand.
    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "config", "config.json"), BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var mode = S(s, "gameMode", "survival");
        return
            $$"""
            {
              "port": {{s.ServerPort}},
              "startCommands": "host {{mode}}"
            }
            """;
    }

    public override string BuildStartArguments(GameServer s) => $"-jar \"{s.InstallPath}\\server.jar\"";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["gameMode"] = "survival",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "gameMode", Label = "Game mode", FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival",
                    Options = ["survival", "sandbox", "attack", "pvp", "hexed"] },
        ]);
        return fields;
    }
}
