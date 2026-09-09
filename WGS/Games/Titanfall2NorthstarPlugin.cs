using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class Titanfall2NorthstarPlugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId           => "titanfall2_northstar";
    public override string GameName         => "Titanfall 2 (Northstar)";
    public override string Description      => "Titanfall 2 dedicated server via Northstar, the community-maintained modding/server framework — requires owning Titanfall 2";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 1237970;
    public override string Executable       => "NorthstarLauncher.exe";
    public override int    DefaultPort      => 37015;
    public override int    DefaultQueryPort => 37015;
    public override int    DefaultMaxPlayers => 24;

    // Northstar isn't on Steam — it's a client/server framework overlaid on a normal owned
    // Titanfall 2 install, applied the same way Schedule I's DedicatedServerMod is: download the
    // latest release zip and extract it over the game files after every install/update.
    public override async Task PostInstallAsync(GameServer server, Action<string> log) => await ApplyNorthstarAsync(server, log);
    public override async Task PostUpdateAsync(GameServer server, Action<string> log) => await ApplyNorthstarAsync(server, log);

    public override Task PreStartAsync(GameServer s)
    {
        // Dedicated mode reads its startup args from ns_startup_args_dedi.txt instead of the
        // normal ns_startup_args.txt — without -softwared3d11 the server refuses to run on a
        // machine with no GPU attached, which is the common case for a headless game server box.
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "ns_startup_args_dedi.txt"), "-dedicated -softwared3d11");
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s) => "-dedicated -softwared3d11";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();

    private static async Task ApplyNorthstarAsync(GameServer server, Action<string> log)
    {
        log("[WGS] Fetching latest Northstar release...");

        string tag, downloadUrl;
        try
        {
            var json = await _http.GetStringAsync("https://api.github.com/repos/R2Northstar/Northstar/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            tag = root.GetProperty("tag_name").GetString() ?? "unknown";

            downloadUrl = "";
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.StartsWith("Northstar.release.", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip"))
                { downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? ""; break; }
            }
            if (string.IsNullOrEmpty(downloadUrl))
                throw new InvalidOperationException("Northstar release zip not found in release assets.");
        }
        catch (Exception ex)
        {
            log($"[ERR] Failed to fetch Northstar release info: {ex.Message}");
            throw;
        }

        log($"[WGS] Downloading Northstar {tag}...");

        var zipPath = Path.Combine(server.InstallPath, "_wgs_northstar.zip");
        try
        {
            var bytes = await _http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, bytes);

            log("[WGS] Applying Northstar...");
            ZipFile.ExtractToDirectory(zipPath, server.InstallPath, overwriteFiles: true);
            log($"[WGS] Northstar {tag} applied.");
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }
}
