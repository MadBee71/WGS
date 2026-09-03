using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class Schedule1Plugin : GamePluginBase, IA2SQueryPlugin
{
    private static readonly HttpClient _http = new();

    public override string GameId           => "schedule1";
    public override string GameName         => "Schedule I";
    public override string Description      => "Dedicated server via DedicatedServerMod — Steam login required to install the game";
    public override string Category         => "Survival";
    public override int    SteamAppId       => 3164500;
    public override int    GameStoreAppId   => 3164500;
    public override string Executable       => "Schedule I.exe";
    public override int    DefaultPort      => 38465;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 16;
    public override bool   RequiresSteamLogin => true;

    public override string BuildStartArguments(GameServer s)
        => "--batchmode --nographics --dedicated-server --stdio-console";

    public override async Task PostInstallAsync(GameServer server, Action<string> log)
    {
        await ApplyDedicatedServerModAsync(server, log);
        WriteSteamAppId(server.InstallPath);
    }

    public override async Task PostUpdateAsync(GameServer server, Action<string> log)
    {
        // Reapply the mod after a game update (the game files are refreshed but mod DLLs survive
        // only if they weren't overwritten; reapplying is safe since the zip just overlays Mods/).
        await ApplyDedicatedServerModAsync(server, log);
        WriteSteamAppId(server.InstallPath);
    }

    public override Task PreStartAsync(GameServer s)
    {
        WriteSteamAppId(s.InstallPath);
        WriteConfigIfMissing(
            Path.Combine(s.InstallPath, "server_config.toml"),
            BuildConfig(s));
        return Task.CompletedTask;
    }

    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void WriteSteamAppId(string installPath)
    {
        var path = Path.Combine(installPath, "steam_appid.txt");
        if (!File.Exists(path))
            File.WriteAllText(path, "3164500");
    }

    private static string BuildConfig(GameServer s) =>
        $"""
        [server]
        serverName = '{(string.IsNullOrWhiteSpace(s.ServerName) ? "Schedule I Server" : s.ServerName).Replace("'", "''")}'
        maxPlayers = {(s.MaxPlayers > 0 ? s.MaxPlayers : 16)}
        serverPort = {(s.ServerPort > 0 ? s.ServerPort : 38465)}
        serverPassword = '{s.ServerPassword.Replace("'", "''")}'

        [storage]
        saveGamePath = ''

        [authentication]
        authProvider = 'SteamGameServer'
        authTimeoutSeconds = 60
        steamGameServerLogOnAnonymous = true
        steamGameServerQueryPort = {(s.QueryPort > 0 ? s.QueryPort : 27016)}
        steamGameServerMode = 'Authentication'

        [messaging]
        messagingBackend = 'FishNetRpc'

        [gameplay]
        pauseGameWhenEmpty = true

        [autosave]
        autoSaveEnabled = true
        autoSaveIntervalMinutes = 5

        [performance]
        targetFrameRate = 60
        vSyncCount = 0
        """;

    private static async Task ApplyDedicatedServerModAsync(GameServer server, Action<string> log)
    {
        log("[WGS] Fetching latest DedicatedServerMod release...");

        string tag, downloadUrl;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/ifBars/S1DedicatedServers/releases/latest");
            req.Headers.Add("User-Agent", "WindowsGameServer");
            using var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            tag = root.GetProperty("tag_name").GetString() ?? "unknown";

            // Prefer Mono-Server.zip (runs on Windows without IL2CPP prerequisites)
            downloadUrl = "";
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.Equals("Mono-Server.zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
                throw new InvalidOperationException("Mono-Server.zip not found in release assets.");
        }
        catch (Exception ex)
        {
            log($"[ERR] Failed to fetch DedicatedServerMod release info: {ex.Message}");
            throw;
        }

        log($"[WGS] Downloading DedicatedServerMod {tag} (Mono)...");

        var zipPath = Path.Combine(server.InstallPath, "_wgs_s1mod.zip");
        try
        {
            var bytes = await _http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, bytes);

            log("[WGS] Applying DedicatedServerMod...");
            ZipFile.ExtractToDirectory(zipPath, server.InstallPath, overwriteFiles: true);
            log($"[WGS] DedicatedServerMod {tag} applied.");
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }
}
