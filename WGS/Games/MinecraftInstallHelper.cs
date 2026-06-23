using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace WGS.Games;

/// <summary>
/// Auto-install helpers for the Minecraft family. Each loader has its own ecosystem:
/// vanilla/Paper are plain jar downloads from public APIs, Forge needs its own installer run
/// as a subprocess, Fabric exposes a ready-to-run server jar directly, and Spigot has to be
/// compiled locally from source via its official BuildTools.jar.
/// </summary>
public static class MinecraftInstallHelper
{
    // Jenkins (hub.spigotmc.org) and some other hosts return 403 for requests with no
    // User-Agent at all, treating them as bots.
    private static readonly HttpClient _http = CreateHttpClient();
    private const string MojangManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public static async Task<string?> GetLatestReleaseVersionAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(MojangManifestUrl);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("latest").GetProperty("release").GetString();
        }
        catch { return null; }
    }

    public static async Task<string?> GetVanillaServerJarUrlAsync(string version)
    {
        try
        {
            var manifestJson = await _http.GetStringAsync(MojangManifestUrl);
            using var manifest = JsonDocument.Parse(manifestJson);
            string? versionUrl = null;
            foreach (var v in manifest.RootElement.GetProperty("versions").EnumerateArray())
            {
                if (v.GetProperty("id").GetString() == version)
                {
                    versionUrl = v.GetProperty("url").GetString();
                    break;
                }
            }
            if (versionUrl == null) return null;

            var versionJson = await _http.GetStringAsync(versionUrl);
            using var versionDoc = JsonDocument.Parse(versionJson);
            return versionDoc.RootElement.GetProperty("downloads").GetProperty("server").GetProperty("url").GetString();
        }
        catch { return null; }
    }

    public static async Task<string?> GetPaperJarUrlAsync(string version)
    {
        try
        {
            var buildsJson = await _http.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{version}/builds");
            using var doc = JsonDocument.Parse(buildsJson);
            var builds = doc.RootElement.GetProperty("builds");
            if (builds.GetArrayLength() == 0) return null;
            var last = builds[builds.GetArrayLength() - 1];
            var build = last.GetProperty("build").GetInt32();
            var jarName = last.GetProperty("downloads").GetProperty("application").GetProperty("name").GetString();
            return $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{build}/downloads/{jarName}";
        }
        catch { return null; }
    }

    public static async Task<string?> GetForgeInstallerUrlAsync(string mcVersion)
    {
        try
        {
            var json = await _http.GetStringAsync("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
            using var doc = JsonDocument.Parse(json);
            var promos = doc.RootElement.GetProperty("promos");

            string? forgeVersion = null;
            foreach (var key in new[] { $"{mcVersion}-recommended", $"{mcVersion}-latest" })
                if (promos.TryGetProperty(key, out var v)) { forgeVersion = v.GetString(); break; }

            if (forgeVersion == null) return null;
            return $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar";
        }
        catch { return null; }
    }

    /// <summary>Fabric exposes a ready-to-run server jar directly via its meta API — no
    /// installer subprocess needed, unlike Forge.</summary>
    public static async Task<string?> GetFabricServerJarUrlAsync(string mcVersion)
    {
        try
        {
            var loaderJson = await _http.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}");
            using var loaderDoc = JsonDocument.Parse(loaderJson);
            var loaders = loaderDoc.RootElement;
            if (loaders.GetArrayLength() == 0) return null;
            var loaderVersion = loaders[0].GetProperty("loader").GetProperty("version").GetString();

            var installerJson = await _http.GetStringAsync("https://meta.fabricmc.net/v2/versions/installer");
            using var installerDoc = JsonDocument.Parse(installerJson);
            var installers = installerDoc.RootElement;
            if (installers.GetArrayLength() == 0) return null;
            var installerVersion = installers[0].GetProperty("version").GetString();

            return $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/{installerVersion}/server/jar";
        }
        catch { return null; }
    }

    public static async Task DownloadFileAsync(string url, string destPath, Action<string> log)
    {
        log($"[Minecraft] Downloading {url} ...");
        using var stream = await _http.GetStreamAsync(url);
        using var file = File.Create(destPath);
        await stream.CopyToAsync(file);
        log("[Minecraft] Download complete.");
    }

    /// <summary>Runs `java -jar jarPath args` in workDir, streaming output through log. Used for
    /// Forge's installer and Spigot's BuildTools — both are real Java subprocesses, not just a
    /// file download.</summary>
    public static async Task<bool> RunJavaAsync(string jarPath, string args, string workDir, Action<string> log, int timeoutMinutes = 10)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "java",
            Arguments = $"-jar \"{jarPath}\" {args}",
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var proc = new Process { StartInfo = psi };

        // BuildTools/installers can spam thousands of lines per second (decompiling classes).
        // The UI's log sink marshals onto the UI thread synchronously per call, so calling it
        // once per line here would hammer the UI thread hard enough to freeze the whole app,
        // including buttons on completely unrelated servers. Batch lines and flush periodically
        // instead of once per line.
        var buffer = new System.Collections.Concurrent.ConcurrentQueue<string>();
        void Flush()
        {
            if (buffer.IsEmpty) return;
            var lines = new List<string>();
            while (buffer.TryDequeue(out var line)) lines.Add(line);
            if (lines.Count > 0) log(string.Join("\n", lines));
        }
        using var flushTimer = new System.Threading.Timer(_ => Flush(), null, 200, 200);

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) buffer.Enqueue(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) buffer.Enqueue(e.Data); };

        try { proc.Start(); }
        catch (Exception ex) { log($"[Minecraft] Couldn't start java — is it installed and on PATH? ({ex.Message})"); return false; }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        var completed = await Task.Run(() => proc.WaitForExit(timeoutMinutes * 60 * 1000));
        Flush();
        if (!completed)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            log("[Minecraft] Process timed out.");
            return false;
        }
        return proc.ExitCode == 0;
    }

    /// <summary>Vanilla Mojang server jars (and everything built on them) refuse to start at all
    /// until the EULA is accepted in this exact file — without it the server silently exits.</summary>
    public static void WriteEulaIfMissing(string installPath)
    {
        var eulaPath = Path.Combine(installPath, "eula.txt");
        if (!File.Exists(eulaPath))
            File.WriteAllText(eulaPath, "eula=true\n");
    }

    /// <summary>Finds the most recently written file matching a glob (e.g. "spigot-*.jar") —
    /// used when the build/install step doesn't let us predict the exact output filename.</summary>
    public static string? FindNewestFile(string dir, string searchPattern)
        => Directory.Exists(dir)
            ? Directory.GetFiles(dir, searchPattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
}
