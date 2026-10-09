using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json.Linq;
using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional Valheim player data through OdinEye (MIT, github.com/sparcopt/odin-eye), a BepInEx server plugin that exposes
/// the connected players over a local REST endpoint. Unlike Steam's A2S query it keeps working with -crossplay, and it
/// gives real names and Steam IDs. WGS installs it only when the server's "OdinEye" setting is on:
///  - BepInEx comes from the BepInExPack_Valheim release pinned below, downloaded from Thunderstore and only when the server
///    folder has no BepInEx yet (an existing BepInEx install and its mods are never touched);
///  - the OdinEye files ship inside WGS (embedded, built from the upstream source) and go to BepInEx\plugins\OdinEye;
///  - the REST server listens on 127.0.0.1 only.
/// </summary>
public static class ValheimOdinEye
{
    public const string BepInExPackVersion = "5.4.2333";
    private const string BepInExPackUrl = "https://old.thunderstore.io/package/download/denikson/BepInExPack_Valheim/" + BepInExPackVersion + "/";
    private const string Marker = ".wgs-managed";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public static bool IsEnabled(GameServer s) => s.GameSpecificSettings.TryGetValue("odinEye", out var v) && v == "true";

    /// <summary>REST port: the "odinEyePort" setting, else game port + 20000 (kept clear of 2456/2457 and of other servers).</summary>
    public static int Port(GameServer s)
        => s.GameSpecificSettings.TryGetValue("odinEyePort", out var v) && int.TryParse(v, out var p) && p is > 1023 and < 65536
            ? p : Math.Min(s.ServerPort + 20000, 65000);

    private static string PluginDir(GameServer s) => Path.Combine(s.InstallPath, "BepInEx", "plugins", "OdinEye");

    /// <summary>Makes the server folder ready: BepInEx (if missing), the OdinEye plugin files and its config. Idempotent.</summary>
    public static async Task EnsureInstalledAsync(GameServer s, Action<string>? log = null)
    {
        var root = Path.GetFullPath(s.InstallPath);
        if (!File.Exists(Path.Combine(root, "BepInEx", "core", "BepInEx.dll")))
        {
            log?.Invoke($"[OdinEye] BepInEx not found — downloading BepInExPack_Valheim {BepInExPackVersion} from Thunderstore...");
            var tmp = Path.Combine(Path.GetTempPath(), $"BepInExPack_Valheim_{BepInExPackVersion}.zip");
            using (var dl = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            await using (var src = await dl.GetStreamAsync(BepInExPackUrl))
            await using (var dst = File.Create(tmp))
                await src.CopyToAsync(dst);

            using (var zip = ZipFile.OpenRead(tmp))
                foreach (var e in zip.Entries)
                {
                    const string prefix = "BepInExPack_Valheim/";
                    if (!e.FullName.StartsWith(prefix, StringComparison.Ordinal) || e.FullName.EndsWith('/')) continue;
                    var target = Path.GetFullPath(Path.Combine(root, e.FullName[prefix.Length..]));
                    if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;   // zip-slip guard
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    e.ExtractToFile(target, overwrite: true);
                }
            try { File.Delete(tmp); } catch { }
            log?.Invoke("[OdinEye] BepInEx installed.");
        }

        var dir = PluginDir(s);
        Directory.CreateDirectory(dir);
        var asm = typeof(ValheimOdinEye).Assembly;
        foreach (var res in asm.GetManifestResourceNames().Where(n => n.StartsWith("WGS.OdinEye.", StringComparison.Ordinal)))
        {
            var name = res["WGS.OdinEye.".Length..];
            await using var rs = asm.GetManifestResourceStream(res)!;
            await using var fs = File.Create(Path.Combine(dir, name));
            await rs.CopyToAsync(fs);
        }
        File.WriteAllText(Path.Combine(dir, Marker), "Installed and managed by WGS. Turning the OdinEye setting off removes this folder.");

        var cfgDir = Path.Combine(root, "BepInEx", "config");
        Directory.CreateDirectory(cfgDir);
        File.WriteAllText(Path.Combine(cfgDir, "org.bepinex.plugins.odineye.cfg"),
            $"[Hosting]\r\n\r\nHttpServerAddress = http://127.0.0.1:{Port(s)}\r\n");
    }

    /// <summary>Removes the plugin WGS installed (only if WGS installed it). BepInEx itself stays: other mods may use it.</summary>
    public static void RemoveIfManaged(GameServer s)
    {
        try
        {
            var dir = PluginDir(s);
            if (File.Exists(Path.Combine(dir, Marker))) Directory.Delete(dir, recursive: true);
        }
        catch { /* best effort */ }
    }

    /// <summary>Connected players from OdinEye, or null when it doesn't answer (not installed, still starting, plugin failed).</summary>
    public static async Task<List<OnlinePlayer>?> GetPlayersAsync(GameServer s)
    {
        try
        {
            var json = await Http.GetStringAsync($"http://127.0.0.1:{Port(s)}/players");
            var list = new List<OnlinePlayer>();
            foreach (var item in JArray.Parse(json))
            {
                string Get(string key) => (item[key] ?? item[char.ToLowerInvariant(key[0]) + key[1..]])?.ToString() ?? "";
                var name = Get("Name");
                list.Add(new OnlinePlayer { Name = string.IsNullOrWhiteSpace(name) ? "?" : name, SteamId = Get("SteamId") });
            }
            return list;
        }
        catch { return null; }
    }
}
