using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Handles downloading and installing mods/frameworks:
///   • Oxide / uMod  — for Rust, 7DTD, and other supported games
///   • Paper          — Minecraft Paper server JAR
/// </summary>
public class ModManagerService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public event Action<string>? StatusChanged;

    // ──────────────────────────────────────────────────────────────────────────
    // OXIDE
    // ──────────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> _oxideGameNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rust"]       = "Rust",
        ["7daystodie"] = "7DaysToDie",
        ["hurtworld"]  = "Hurtworld",
        ["theforest"]  = "TheForest",
        ["scum"]       = "SCUM",
    };

    /// <summary>Returns true if we know the uMod game name for this plugin.</summary>
    public static bool SupportsOxide(IGamePlugin plugin)
        => plugin.SupportsOxide && _oxideGameNames.ContainsKey(plugin.GameId);

    /// <summary>Download and extract the latest Oxide build into the server's install folder.</summary>
    public async Task InstallOxideAsync(IGamePlugin plugin, string installPath,
                                        IProgress<(int pct, string msg)>? progress = null)
    {
        if (!_oxideGameNames.TryGetValue(plugin.GameId, out var oxName))
            throw new InvalidOperationException($"Oxide not supported for {plugin.GameName}");

        // uMod releases API
        var apiUrl  = $"https://umod.org/games/{oxName.ToLower()}/download";
        Report(progress, 0, $"Fetching Oxide for {plugin.GameName}...");

        var bytes = await _http.GetByteArrayAsync(apiUrl);
        Report(progress, 50, "Extracting Oxide...");

        var tempZip = Path.Combine(Path.GetTempPath(), $"oxide_{plugin.GameId}_{Guid.NewGuid()}.zip");
        try
        {
            await File.WriteAllBytesAsync(tempZip, bytes);
            Directory.CreateDirectory(installPath);
            ZipFile.ExtractToDirectory(tempZip, installPath, overwriteFiles: true);
        }
        finally
        {
            try { File.Delete(tempZip); } catch { }
        }

        Report(progress, 100, $"✅ Oxide installed for {plugin.GameName}");
    }

    /// <summary>Returns the Oxide version string from CSharp.dll in the install folder, or null.</summary>
    public static string? GetInstalledOxideVersion(string installPath)
    {
        var dll = Path.Combine(installPath, "RustDedicated_Data", "Managed", "Oxide.Core.dll");
        if (!File.Exists(dll))
            dll = Path.Combine(installPath, "oxide", "Oxide.Core.dll");
        if (!File.Exists(dll)) return null;
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(dll);
            return info.ProductVersion ?? info.FileVersion;
        }
        catch { return null; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PAPER (Minecraft)
    // ──────────────────────────────────────────────────────────────────────────

    private const string PaperApiBase = "https://api.papermc.io/v2/projects/paper";

    public record PaperVersionInfo(string Version, int Build, string DownloadUrl);

    public async Task<PaperVersionInfo?> GetLatestPaperAsync()
    {
        try
        {
            // 1. Get project info → list of versions
            var projJson = await _http.GetStringAsync(PaperApiBase);
            using var proj = JsonDocument.Parse(projJson);
            var versions  = proj.RootElement.GetProperty("versions")
                               .EnumerateArray()
                               .Select(v => v.GetString()!)
                               .ToList();
            if (versions.Count == 0) return null;
            var latest = versions.Last();

            // 2. Get builds for that version
            var buildsJson = await _http.GetStringAsync($"{PaperApiBase}/versions/{latest}/builds");
            using var builds = JsonDocument.Parse(buildsJson);
            var buildArr = builds.RootElement.GetProperty("builds").EnumerateArray().ToList();
            if (buildArr.Count == 0) return null;

            var lastBuild = buildArr.Last();
            var buildNum  = lastBuild.GetProperty("build").GetInt32();
            var fileName  = lastBuild.GetProperty("downloads")
                                     .GetProperty("application")
                                     .GetProperty("name").GetString()!;

            var url = $"{PaperApiBase}/versions/{latest}/builds/{buildNum}/downloads/{fileName}";
            return new PaperVersionInfo(latest, buildNum, url);
        }
        catch { return null; }
    }

    public async Task InstallPaperAsync(string installPath,
                                        IProgress<(int pct, string msg)>? progress = null)
    {
        Report(progress, 0, "Fetching latest Paper version info...");
        var info = await GetLatestPaperAsync()
                   ?? throw new InvalidOperationException("Could not fetch Paper version info from papermc.io");

        Report(progress, 10, $"Downloading Paper {info.Version} build {info.Build}...");
        var bytes = await _http.GetByteArrayAsync(info.DownloadUrl);

        Directory.CreateDirectory(installPath);
        var destJar = Path.Combine(installPath, "server.jar");
        await File.WriteAllBytesAsync(destJar, bytes);

        // Write eula.txt so the server can start
        var eulaPath = Path.Combine(installPath, "eula.txt");
        if (!File.Exists(eulaPath))
            await File.WriteAllTextAsync(eulaPath, "#Generated by WGS\neula=true\n");

        Report(progress, 100, $"✅ Paper {info.Version} build {info.Build} installed");
    }

    /// <summary>Reads Paper version from version_history.json written by Paper on first run.</summary>
    public static string? GetInstalledPaperVersion(string installPath)
    {
        var f = Path.Combine(installPath, "version_history.json");
        if (!File.Exists(f)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            return doc.RootElement.GetProperty("currentVersion").GetString();
        }
        catch { return null; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PURPUR (Paper fork with extra features)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task InstallPurpurAsync(string installPath,
                                         IProgress<(int pct, string msg)>? progress = null)
    {
        Report(progress, 0, "Fetching latest Purpur version...");
        // Get latest MC version
        var versionsJson = await _http.GetStringAsync("https://api.purpurmc.org/v2/purpur");
        using var versionsDoc = JsonDocument.Parse(versionsJson);
        var latestVer = versionsDoc.RootElement
                            .GetProperty("versions").EnumerateArray()
                            .Select(v => v.GetString()!).Last();

        Report(progress, 10, $"Fetching latest Purpur build for {latestVer}...");
        var buildJson = await _http.GetStringAsync($"https://api.purpurmc.org/v2/purpur/{latestVer}/latest");
        using var buildDoc = JsonDocument.Parse(buildJson);
        var build = buildDoc.RootElement.GetProperty("build").GetString()!;

        var url = $"https://api.purpurmc.org/v2/purpur/{latestVer}/{build}/download";
        Report(progress, 20, $"Downloading Purpur {latestVer} build {build}...");
        var bytes = await _http.GetByteArrayAsync(url);

        Directory.CreateDirectory(installPath);
        await File.WriteAllBytesAsync(Path.Combine(installPath, "server.jar"), bytes);
        EnsureEula(installPath);
        Report(progress, 100, $"✅ Purpur {latestVer} build {build} installed");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FABRIC
    // ──────────────────────────────────────────────────────────────────────────

    public async Task InstallFabricAsync(string installPath,
                                         IProgress<(int pct, string msg)>? progress = null)
    {
        Report(progress, 0, "Fetching latest Fabric loader version...");

        // 1. Get latest stable MC version
        var mcJson = await _http.GetStringAsync("https://meta.fabricmc.net/v2/versions/game");
        using var mcDoc = JsonDocument.Parse(mcJson);
        var latestMc = mcDoc.RootElement.EnumerateArray()
                            .Where(v => v.GetProperty("stable").GetBoolean())
                            .Select(v => v.GetProperty("version").GetString()!)
                            .First();

        // 2. Get latest stable loader
        var loaderJson = await _http.GetStringAsync("https://meta.fabricmc.net/v2/versions/loader");
        using var loaderDoc = JsonDocument.Parse(loaderJson);
        var latestLoader = loaderDoc.RootElement.EnumerateArray()
                                    .Where(v => v.GetProperty("stable").GetBoolean())
                                    .Select(v => v.GetProperty("version").GetString()!)
                                    .First();

        // 3. Get latest installer
        var installerJson = await _http.GetStringAsync("https://meta.fabricmc.net/v2/versions/installer");
        using var installerDoc = JsonDocument.Parse(installerJson);
        var latestInstaller = installerDoc.RootElement.EnumerateArray()
                                          .Where(v => v.GetProperty("stable").GetBoolean())
                                          .Select(v => v.GetProperty("version").GetString()!)
                                          .First();

        // 4. Download the server launch JAR directly (no installer needed — Fabric provides a launcher jar)
        var url = $"https://meta.fabricmc.net/v2/versions/loader/{latestMc}/{latestLoader}/{latestInstaller}/server/jar";
        Report(progress, 20, $"Downloading Fabric {latestMc} (loader {latestLoader})...");
        var bytes = await _http.GetByteArrayAsync(url);

        Directory.CreateDirectory(installPath);
        await File.WriteAllBytesAsync(Path.Combine(installPath, "server.jar"), bytes);
        EnsureEula(installPath);
        Report(progress, 100, $"✅ Fabric {latestMc} loader {latestLoader} installed");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // VANILLA (official Mojang server)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task InstallVanillaAsync(string installPath,
                                          IProgress<(int pct, string msg)>? progress = null)
    {
        Report(progress, 0, "Fetching Minecraft version manifest...");
        var manifestJson = await _http.GetStringAsync(
            "https://launchermeta.mojang.com/mc/game/version_manifest.json");
        using var manifest = JsonDocument.Parse(manifestJson);

        var latestRelease = manifest.RootElement
                                    .GetProperty("latest")
                                    .GetProperty("release")
                                    .GetString()!;

        var versionUrl = manifest.RootElement
                                 .GetProperty("versions").EnumerateArray()
                                 .First(v => v.GetProperty("id").GetString() == latestRelease)
                                 .GetProperty("url").GetString()!;

        Report(progress, 10, $"Fetching version info for {latestRelease}...");
        var verJson = await _http.GetStringAsync(versionUrl);
        using var verDoc = JsonDocument.Parse(verJson);
        var serverUrl = verDoc.RootElement
                              .GetProperty("downloads")
                              .GetProperty("server")
                              .GetProperty("url").GetString()!;

        Report(progress, 20, $"Downloading Vanilla {latestRelease} server...");
        var bytes = await _http.GetByteArrayAsync(serverUrl);

        Directory.CreateDirectory(installPath);
        await File.WriteAllBytesAsync(Path.Combine(installPath, "server.jar"), bytes);
        EnsureEula(installPath);
        Report(progress, 100, $"✅ Vanilla Minecraft {latestRelease} server installed");
    }

    private static void EnsureEula(string installPath)
    {
        var eulaPath = Path.Combine(installPath, "eula.txt");
        if (!File.Exists(eulaPath))
            File.WriteAllText(eulaPath, "#Generated by WGS — you accept the Minecraft EULA\neula=true\n");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Installed plugins/mods listing
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Returns (name, size) tuples for Oxide plugins in [installPath]/oxide/plugins/.</summary>
    public static List<(string Name, long Bytes)> ListOxidePlugins(string installPath)
    {
        var dir = Path.Combine(installPath, "oxide", "plugins");
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.cs")
            .Concat(Directory.GetFiles(dir, "*.dll"))
            .Select(f => (Path.GetFileName(f), new FileInfo(f).Length))
            .OrderBy(x => x.Item1)
            .ToList();
    }

    /// <summary>Returns (name, size) tuples for Minecraft plugin JARs in [installPath]/plugins/.</summary>
    public static List<(string Name, long Bytes)> ListMinecraftPlugins(string installPath)
    {
        var dir = Path.Combine(installPath, "plugins");
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.jar")
            .Select(f => (Path.GetFileName(f), new FileInfo(f).Length))
            .OrderBy(x => x.Item1)
            .ToList();
    }

    /// <summary>Opens the appropriate plugins folder in Explorer. Creates it if needed.</summary>
    public static void OpenPluginFolder(IGamePlugin plugin, string installPath)
    {
        string dir;
        if (plugin.SupportsOxide)
            dir = Path.Combine(installPath, "oxide", "plugins");
        else if (!string.IsNullOrEmpty(plugin.MinecraftFlavor))
            dir = Path.Combine(installPath, "plugins");
        else
            dir = installPath;

        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start("explorer.exe", dir);
    }

    private void Report(IProgress<(int, string)>? p, int pct, string msg)
    {
        StatusChanged?.Invoke(msg);
        p?.Report((pct, msg));
    }
}
