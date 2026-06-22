using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace WGS.Games;

/// <summary>
/// Looks up the latest FXServer build download URL from Cfx.re's own changelog API.
/// Both FiveM and RedM run the exact same fxserver.exe binary (confirmed: same artifact,
/// same Cfx.re base server, only the game-specific data/config differs) — same lookup for both.
/// </summary>
public static class CfxArtifactHelper
{
    private static readonly HttpClient _http = new();
    private const string ChangelogUrl = "https://changelogs-live.fivem.net/api/changelog/versions/win32/server";

    // The FXServer.exe artifact does NOT bundle mapmanager/chat/spawnmanager/etc — those base
    // resources have to be fetched separately from Cfx.re's own data repo (same thing txAdmin's
    // "Popular Recipes" wizard does via its download_github step). Without this, every `ensure`
    // line in server.cfg fails with "Couldn't find resource X" and nothing loads.
    private const string BaseResourcesZipUrl = "https://github.com/citizenfx/cfx-server-data/archive/refs/heads/master.zip";

    /// <summary>Downloads and unpacks Cfx.re's base resources (mapmanager, chat, spawnmanager, etc.)
    /// into {installPath}/resources if they aren't already there. Safe to call on every start —
    /// it's a no-op once the marker resource folder exists. Swallows errors so a network hiccup
    /// doesn't block server start; the existing "Couldn't find resource" log lines still surface
    /// the problem if this silently fails.</summary>
    public static async Task EnsureBaseResourcesAsync(string installPath)
    {
        var resourcesDir = Path.Combine(installPath, "resources");
        var marker = Path.Combine(resourcesDir, "mapmanager");
        if (Directory.Exists(marker)) return;

        var tempZip = Path.Combine(Path.GetTempPath(), $"cfx-server-data-{Guid.NewGuid():N}.zip");
        var tempExtract = Path.Combine(Path.GetTempPath(), $"cfx-server-data-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(resourcesDir);

            using (var stream = await _http.GetStreamAsync(BaseResourcesZipUrl))
            using (var file = File.Create(tempZip))
                await stream.CopyToAsync(file);

            ZipFile.ExtractToDirectory(tempZip, tempExtract);

            // Archive extracts to a single "cfx-server-data-<branch>" root folder containing "resources/"
            var rootFolder = Directory.GetDirectories(tempExtract).FirstOrDefault();
            var sourceResources = rootFolder != null ? Path.Combine(rootFolder, "resources") : null;
            if (sourceResources == null || !Directory.Exists(sourceResources)) return;

            CopyDirectory(sourceResources, resourcesDir);
        }
        catch { /* network hiccup — leave existing "Couldn't find resource" logging to surface it */ }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, recursive: true); } catch { }
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(sourceDir, destDir));
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(sourceDir, destDir), overwrite: true);
    }

    /// <summary>txAdmin (bundled in every FXServer build) ignores server.cfg entirely unless its own
    /// profile config.json points "server.dataPath" at the folder containing it — normally set up
    /// through a one-time browser setup wizard. Pre-seeding this file lets WGS skip that wizard
    /// entirely: txAdmin sees an already-configured "default" profile and goes straight to running
    /// the server.cfg WGS already wrote. Only written if missing, so we never clobber a profile the
    /// user (or txAdmin itself) already set up.</summary>
    public static void EnsureTxAdminProfile(string dataPath)
    {
        var profileDir  = Path.Combine(dataPath, "txData", "default");
        var configPath  = Path.Combine(profileDir, "config.json");
        if (File.Exists(configPath)) return;

        Directory.CreateDirectory(profileDir);
        var normalizedPath = dataPath.Replace('\\', '/').TrimEnd('/') + "/";
        var json =
            $$"""
            {
              "version": 2,
              "server": {
                "dataPath": "{{normalizedPath}}"
              }
            }
            """;
        File.WriteAllText(configPath, json);
    }

    public record ArtifactInfo(string Build, string DownloadUrl);

    public static async Task<string?> GetLatestServerDownloadUrlAsync()
        => (await GetLatestAsync())?.DownloadUrl;

    /// <summary>The newest build — gets new features fastest, but per Cfx.re's own guidance can be buggy.</summary>
    public static Task<ArtifactInfo?> GetLatestAsync() => GetAsync("latest", "latest_download");

    /// <summary>The build Cfx.re currently recommends for production use — slower to get new features, more stable.</summary>
    public static Task<ArtifactInfo?> GetRecommendedAsync() => GetAsync("recommended", "recommended_download");

    private static async Task<ArtifactInfo?> GetAsync(string buildField, string urlField)
    {
        try
        {
            var json = await _http.GetStringAsync(ChangelogUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty(buildField, out var build) || !root.TryGetProperty(urlField, out var url))
                return null;
            var buildStr = build.GetString();
            var urlStr   = url.GetString();
            return buildStr != null && urlStr != null ? new ArtifactInfo(buildStr, urlStr) : null;
        }
        catch { return null; }
    }
}
