using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Installs Thunderstore packages into a BepInEx game server folder, following the same file placement rules as
/// r2modman (InstallRulePluginInstaller / BepInExInstaller), so a mod lands where its author expects it.
/// Differences, on purpose, because this is a server whose owner has tuned it: files already in BepInEx\config are
/// never overwritten, and only files WGS itself wrote are ever removed or disabled.
/// The caller is responsible for the server being stopped.
/// </summary>
public class ThunderstoreService
{
    private const string Api = "https://thunderstore.io";
    private const string StateFile = @"BepInEx\wgs-thunderstore.json";
    private const string ConfigRoute = "BepInEx/config";
    private const string TempFolder = ".wgs-thunderstore-tmp";

    // Folder names inside a package that map to a fixed BepInEx location.
    private static readonly Dictionary<string, string> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["plugins"]  = "BepInEx/plugins",
        ["core"]     = "BepInEx/core",
        ["patchers"] = "BepInEx/patchers",
        ["monomod"]  = "BepInEx/monomod",
        ["config"]   = ConfigRoute,
        ["SlimVML"]  = "BepInEx/SlimVML",
    };

    private static readonly JsonSerializerOptions JsonOut = new() { WriteIndented = true };
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _http;

    public ThunderstoreService() : this(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { }

    public ThunderstoreService(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsGameServer/" + (typeof(ThunderstoreService).Assembly.GetName().Version?.ToString(3) ?? "1.0"));
    }

    // ── What the user typed ──────────────────────────────────────────────────

    public sealed record Request(string? Namespace, string? Name, string? Version, string? ProfileCode, string? Community)
    {
        public string Id => $"{Namespace}-{Name}";
    }

    private static readonly Regex GuidRx = new(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase);
    private static readonly Regex DependencyRx = new(@"^(?<ns>[A-Za-z0-9_.\-]+?)-(?<name>[A-Za-z0-9_]+)(-(?<ver>\d+\.\d+\.\d+))?$");

    /// <summary>
    /// Accepts a Thunderstore package page or download link, an "Install with Mod Manager" (ror2mm://) link,
    /// "Namespace-Name[-1.2.3]", or an r2modman profile code.
    /// </summary>
    public static Request Parse(string input)
    {
        var text = (input ?? "").Trim();
        if (text.Length == 0) throw new InvalidOperationException("Paste a Thunderstore link, a Namespace-Name or an r2modman profile code.");
        if (GuidRx.IsMatch(text)) return new Request(null, null, null, text, null);

        if (text.Contains("://"))
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) throw new InvalidOperationException("That link could not be read.");
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
            if (uri.Scheme == "ror2mm")   // ror2mm://v1/install/thunderstore.io/Namespace/Name/1.2.3/
            {
                if (parts.Length >= 5 && parts[0] == "install") return Checked(parts[2], parts[3], parts[4], null);
                throw new InvalidOperationException("That install link could not be read.");
            }
            if (uri.Host != "thunderstore.io" && !uri.Host.EndsWith(".thunderstore.io", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only thunderstore.io links are supported.");

            string? community = null;
            var sub = uri.Host.Split('.');
            if (sub.Length == 3 && sub[0] is not ("www" or "old" or "new" or "gcdn" or "cdn")) community = sub[0];

            // /c/{community}/p/{ns}/{name}[/v/{version}]
            if (parts.Length >= 5 && parts[0] == "c" && parts[2] == "p")
                return Checked(parts[3], parts[4], parts.Length >= 7 && parts[5] == "v" ? parts[6] : null, parts[1]);
            // /package/download/{ns}/{name}/{version}
            if (parts.Length >= 5 && parts[0] == "package" && parts[1] == "download")
                return Checked(parts[2], parts[3], parts[4], community);
            // /package/{ns}/{name}[/{version}]
            if (parts.Length >= 3 && parts[0] == "package")
                return Checked(parts[1], parts[2], parts.Length >= 4 && Regex.IsMatch(parts[3], @"^\d+\.\d+\.\d+$") ? parts[3] : null, community);
            throw new InvalidOperationException("That is not a package link. Open the mod's own page and copy its address.");
        }

        var m = DependencyRx.Match(text);
        if (!m.Success) throw new InvalidOperationException("Not recognised. Use a package link, Namespace-Name or an r2modman profile code.");
        return Checked(m.Groups["ns"].Value, m.Groups["name"].Value, m.Groups["ver"].Success ? m.Groups["ver"].Value : null, null);
    }

    private static Request Checked(string ns, string name, string? version, string? community)
    {
        // These end up in folder names and URLs.
        if (!Regex.IsMatch(ns, @"^[A-Za-z0-9_][A-Za-z0-9_.\-]*$") || !Regex.IsMatch(name, @"^[A-Za-z0-9_]+$")
            || (version != null && !Regex.IsMatch(version, @"^\d+\.\d+\.\d+$")))
            throw new InvalidOperationException("Invalid package name.");
        return new Request(ns, name, version, null, community);
    }

    // ── File placement (pure) ────────────────────────────────────────────────

    /// <param name="Entry">Zip entry name as stored in the zip.</param>
    /// <param name="Target">Destination relative to the server folder, with '/' separators.</param>
    /// <param name="IsConfig">Config files are only written when missing, and are never removed.</param>
    public sealed record PlacedFile(string Entry, string Target, bool IsConfig);

    public static List<PlacedFile> PlanPackage(IEnumerable<string> entryNames, string modId)
    {
        var result = new Dictionary<string, PlacedFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var (entry, parts) in SafeEntries(entryNames))
        {
            // The first folder whose name is a known route decides; folders above it are wrappers and are dropped.
            var routeAt = -1;
            for (var i = 0; i < parts.Length - 1 && routeAt < 0; i++)
                if (Routes.ContainsKey(parts[i])) routeAt = i;

            PlacedFile placed;
            if (routeAt >= 0)
            {
                var route = Routes[parts[routeAt]];
                var rest = string.Join('/', parts[(routeAt + 1)..]);
                placed = route == ConfigRoute
                    ? new PlacedFile(entry, $"{route}/{rest}", true)
                    : new PlacedFile(entry, $"{route}/{modId}/{rest}", false);
            }
            else
            {
                // Loose file (at any depth): flattened into the mod's own folder.
                var file = parts[^1];
                var route = file.EndsWith(".mm.dll", StringComparison.OrdinalIgnoreCase) ? Routes["monomod"] : Routes["plugins"];
                placed = new PlacedFile(entry, $"{route}/{modId}/{file}", false);
            }
            result[placed.Target] = placed;
        }
        return result.Values.ToList();
    }

    public static List<PlacedFile> PlanLoader(IEnumerable<string> entryNames, string rootFolder)
    {
        var result = new List<PlacedFile>();
        foreach (var (entry, parts) in SafeEntries(entryNames))
        {
            if (parts.Length < 2 || !parts[0].Equals(rootFolder, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = parts[1..];
            if (rest.Length == 1 && rest[0].ToLowerInvariant() is "manifest.json" or "readme.md" or "icon.png") continue;
            var target = string.Join('/', rest);
            result.Add(new PlacedFile(entry, target, target.StartsWith(ConfigRoute + "/", StringComparison.OrdinalIgnoreCase)));
        }
        return result;
    }

    private static IEnumerable<(string Entry, string[] Parts)> SafeEntries(IEnumerable<string> entryNames)
    {
        foreach (var entry in entryNames)
        {
            var path = entry.Replace('\\', '/');   // some packages are zipped with Windows separators
            if (path.EndsWith('/')) continue;
            var parts = path.Split('/');
            if (parts.Any(p => p.Length == 0 || p == "." || p == ".." || p.Contains(':')
                               || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new InvalidOperationException($"The package contains an unsafe file path ({entry}); it was not installed.");
            yield return (entry, parts);
        }
    }

    // ── Installed state ──────────────────────────────────────────────────────

    public List<ThunderstoreMod> GetInstalled(GameServer server)
    {
        var file = Path.Combine(server.InstallPath, StateFile);
        if (!File.Exists(file)) return [];
        try { return JsonSerializer.Deserialize<List<ThunderstoreMod>>(File.ReadAllText(file)) ?? []; }
        catch (JsonException ex) { throw new InvalidOperationException($"{file} is damaged: {ex.Message}"); }
    }

    private static void SaveState(string root, Dictionary<string, ThunderstoreMod> mods)
    {
        var file = Path.Combine(root, StateFile);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var list = mods.Values.OrderByDescending(m => m.IsLoader).ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Select(m => new ThunderstoreMod
            {
                Id = m.Id, Version = m.Version, Enabled = m.Enabled, Requested = m.Requested,
                IsLoader = m.IsLoader, Dependencies = m.Dependencies, Files = m.Files,
            }).ToList();
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(list, JsonOut));
        File.Move(file + ".tmp", file, overwrite: true);
    }

    private Dictionary<string, ThunderstoreMod> LoadState(GameServer server)
        => GetInstalled(server).ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

    private static string FullPath(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to touch a file outside the server folder: {relative}");
        return full;
    }

    private static bool IsPresent(string root, ThunderstoreMod mod)
        => mod.Files.Any(f => File.Exists(FullPath(root, f)) || File.Exists(FullPath(root, f) + ".old"));

    /// <summary>True while mods of this server folder are being changed (starting the server then would be unsafe).</summary>
    public static bool IsBusy(string installPath)
        => !string.IsNullOrWhiteSpace(installPath)
           && Locks.TryGetValue(Path.GetFullPath(installPath).TrimEnd(Path.DirectorySeparatorChar), out var gate)
           && gate.CurrentCount == 0;

    private async Task<T> LockedAsync<T>(GameServer server, Func<string, Task<T>> action)
    {
        if (string.IsNullOrWhiteSpace(server.InstallPath) || !Directory.Exists(server.InstallPath))
            throw new InvalidOperationException("Install the game server first.");
        var root = Path.GetFullPath(server.InstallPath).TrimEnd(Path.DirectorySeparatorChar);
        var gate = Locks.GetOrAdd(root, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try { return await action(root); }
        finally
        {
            try { var temp = Path.Combine(root, TempFolder); if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
            catch { /* best effort */ }
            gate.Release();
        }
    }

    // ── Metadata ─────────────────────────────────────────────────────────────

    private sealed record VersionInfo(string Id, string Namespace, string Name, string Version, List<string> Dependencies, string DownloadUrl);
    private sealed record PackageInfo(VersionInfo Latest, bool Deprecated, List<(string Community, List<string> Categories)> Listings);

    /// <summary>GET that waits and retries when Thunderstore says it is being asked too often (large profiles hit this).</summary>
    private async Task<HttpResponseMessage> GetAsync(string url, string? notFound)
    {
        for (var attempt = 1; ; attempt++)
        {
            var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var wait = resp.Headers.RetryAfter?.Delta
                    ?? (resp.Headers.RetryAfter?.Date is { } at ? at - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(Math.Min(60, 5 << (attempt - 1))));
                resp.Dispose();
                if (attempt >= 7 || wait > TimeSpan.FromMinutes(2))
                    throw new InvalidOperationException("Thunderstore is rate limiting requests. Wait a few minutes and try again.");
                await Task.Delay(wait > TimeSpan.FromSeconds(1) ? wait : TimeSpan.FromSeconds(1));
                continue;
            }
            if (resp.StatusCode == HttpStatusCode.NotFound && notFound != null)
            {
                resp.Dispose();
                throw new InvalidOperationException(notFound);
            }
            resp.EnsureSuccessStatusCode();
            return resp;
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string notFound)
    {
        using var resp = await GetAsync(url, notFound);
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    }

    private static string TempZip(string root, string id, string version) => Path.Combine(root, TempFolder, $"{id}-{version}.zip");

    private async Task DownloadAsync(string url, string path, string notFound)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var resp = await GetAsync(url, notFound);
        await using (var file = File.Create(path + ".part"))
            await resp.Content.CopyToAsync(file);
        File.Move(path + ".part", path, overwrite: true);
    }

    /// <summary>
    /// A package at an exact version: downloads it and reads the dependencies from its own manifest.json, so pinned
    /// versions (profiles, modpacks) need no separate metadata request each.
    /// </summary>
    private async Task<VersionInfo> FetchExactAsync(string root, string ns, string name, string version)
    {
        Checked(ns, name, version, null);
        var id = $"{ns}-{name}";
        var url = $"{Api}/package/download/{ns}/{name}/{version}/";
        var path = TempZip(root, id, version);
        await DownloadAsync(url, path, $"Thunderstore has no version {version} of {id}.");

        using var zip = ZipFile.OpenRead(path);
        var manifest = zip.Entries.FirstOrDefault(e => e.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{id} has no manifest.json.");
        using var reader = new StreamReader(manifest.Open(), Encoding.UTF8);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync(),
            new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var dependencies = doc.RootElement.TryGetProperty("dependencies", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(d => d.GetString()!).ToList()
            : [];
        return new VersionInfo(id, ns, name, version, dependencies, url);
    }

    private static VersionInfo ReadVersion(JsonElement e)
    {
        var ns = e.GetProperty("namespace").GetString()!;
        var name = e.GetProperty("name").GetString()!;
        var version = e.GetProperty("version_number").GetString()!;
        Checked(ns, name, version, null);
        return new VersionInfo($"{ns}-{name}", ns, name, version,
            e.GetProperty("dependencies").EnumerateArray().Select(d => d.GetString()!).ToList(),
            e.GetProperty("download_url").GetString()!);
    }

    private async Task<PackageInfo> GetPackageAsync(string ns, string name)
    {
        using var doc = await GetJsonAsync($"{Api}/api/experimental/package/{ns}/{name}/", $"Thunderstore has no package {ns}-{name}.");
        var root = doc.RootElement;
        var listings = root.GetProperty("community_listings").EnumerateArray()
            .Select(l => (l.GetProperty("community").GetString() ?? "",
                          l.GetProperty("categories").EnumerateArray().Select(c => c.GetString() ?? "").ToList()))
            .ToList();
        return new PackageInfo(ReadVersion(root.GetProperty("latest")),
            root.TryGetProperty("is_deprecated", out var dep) && dep.GetBoolean(), listings);
    }


    private static (string Namespace, string Name, string Version) SplitDependency(string dependency)
    {
        var m = DependencyRx.Match(dependency);
        if (!m.Success || !m.Groups["ver"].Success) throw new InvalidOperationException($"Unreadable dependency: {dependency}");
        return (m.Groups["ns"].Value, m.Groups["name"].Value, m.Groups["ver"].Value);
    }

    private static string DependencyId(string dependency)
    {
        var d = SplitDependency(dependency);
        return $"{d.Namespace}-{d.Name}";
    }

    // ── Install ──────────────────────────────────────────────────────────────

    private sealed class Wanted
    {
        public required VersionInfo Info;
        public bool Requested;
        public bool Enabled = true;
    }

    public Task<List<string>> InstallAsync(GameServer server, ThunderstoreGame game, string input, IProgress<(int pct, string msg)>? progress = null)
    {
        var request = Parse(input);
        if (request.Community != null && !request.Community.Equals(game.Community, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"That link is for the '{request.Community}' community, not {game.Community}.");

        return LockedAsync(server, async root =>
        {
            var log = new List<string>();
            var installed = LoadState(server);
            progress?.Report((0, "Reading package information from Thunderstore..."));

            if (request.ProfileCode != null)
            {
                await InstallProfileAsync(root, game, installed, request.ProfileCode, log, progress);
                return log;
            }

            var package = await GetPackageAsync(request.Namespace!, request.Name!);
            var listing = package.Listings.FirstOrDefault(l => l.Community.Equals(game.Community, StringComparison.OrdinalIgnoreCase));
            if (listing.Community == null)
                throw new InvalidOperationException($"{request.Id} is not listed for {game.Community} on Thunderstore.");
            if (package.Deprecated) log.Add($"Note: {package.Latest.Id} is marked deprecated on Thunderstore.");

            var main = request.Version == null || request.Version == package.Latest.Version
                ? package.Latest
                : await FetchExactAsync(root, request.Namespace!, request.Name!, request.Version);
            var isModpack = listing.Categories.Contains("Modpacks");

            // Same version choice as r2modman: a modpack pins the exact versions it lists; a normal mod takes the
            // newest version of each missing dependency and leaves installed ones alone.
            var wanted = new Dictionary<string, Wanted>(StringComparer.OrdinalIgnoreCase)
            {
                [main.Id] = new Wanted { Info = main, Requested = true },
            };
            var queue = new Queue<VersionInfo>();
            queue.Enqueue(main);
            while (queue.Count > 0)
            {
                foreach (var dependency in queue.Dequeue().Dependencies)
                {
                    var (ns, name, version) = SplitDependency(dependency);
                    var id = $"{ns}-{name}";
                    if (wanted.ContainsKey(id)) continue;
                    var have = installed.GetValueOrDefault(id);
                    var present = have != null && IsPresent(root, have);
                    if (present && (!isModpack || have!.Version == version))
                    {
                        if (!have!.Enabled) { SetEnabled(root, have, true); log.Add($"Enabled {have.Id} (needed as a dependency)."); }
                        continue;
                    }
                    if (have == null && id.Equals(game.LoaderPackage, StringComparison.OrdinalIgnoreCase) && HasUnmanagedLoader(root))
                        continue;   // noted below
                    // A modpack pinning an old loader must not downgrade the one already installed.
                    if (present && id.Equals(game.LoaderPackage, StringComparison.OrdinalIgnoreCase)
                        && Version.TryParse(have!.Version, out var current) && Version.TryParse(version, out var pinned) && current > pinned)
                        continue;
                    var info = isModpack ? await FetchExactAsync(root, ns, name, version) : (await GetPackageAsync(ns, name)).Latest;
                    wanted[id] = new Wanted { Info = info };
                    queue.Enqueue(info);
                    if (wanted.Count > 600) throw new InvalidOperationException("The dependency list is unreasonably large; nothing was installed.");
                }
            }
            // Every package needs the loader, even when its manifest forgets to say so.
            if (!wanted.ContainsKey(game.LoaderPackage) && !installed.ContainsKey(game.LoaderPackage) && !HasUnmanagedLoader(root))
                wanted[game.LoaderPackage] = new Wanted { Info = (await GetPackageAsync(game.LoaderPackage.Split('-')[0], game.LoaderPackage.Split('-')[1])).Latest };

            await ApplyAsync(root, game, installed, wanted, log, progress);

            if (!installed.ContainsKey(game.LoaderPackage) && HasUnmanagedLoader(root))
                log.Add($"Kept the BepInEx that was already in the server folder. If mods fail to load, add {game.LoaderPackage} here to update it.");
            return log;
        });
    }

    private static bool HasUnmanagedLoader(string root) => File.Exists(Path.Combine(root, "BepInEx", "core", "BepInEx.dll"));

    /// <summary>Downloads and checks everything first, so a failed download leaves the server folder as it was.</summary>
    private async Task ApplyAsync(string root, ThunderstoreGame game, Dictionary<string, ThunderstoreMod> installed,
        Dictionary<string, Wanted> wanted, List<string> log, IProgress<(int pct, string msg)>? progress)
    {
        // Packages already installed at the wanted version only need their flags updated.
        var todo = new List<Wanted>();
        foreach (var w in wanted.Values)
        {
            if (installed.TryGetValue(w.Info.Id, out var have) && have.Version == w.Info.Version && IsPresent(root, have))
            {
                have.Requested |= w.Requested;
                if (have.Enabled != w.Enabled && !have.IsLoader) SetEnabled(root, have, w.Enabled);
                if (w.Requested) log.Add($"{have.Id} {have.Version} is already installed.");
                continue;
            }
            todo.Add(w);
        }

        var plans = new List<(Wanted Wanted, string Zip, List<PlacedFile> Files, bool IsLoader)>();
        for (var i = 0; i < todo.Count; i++)
        {
            var info = todo[i].Info;
            progress?.Report((i * 80 / todo.Count, $"Downloading {info.Id} {info.Version} ({i + 1}/{todo.Count})..."));
            var zipPath = TempZip(root, info.Id, info.Version);
            await DownloadAsync(info.DownloadUrl, zipPath, $"{info.Id} {info.Version} could not be downloaded.");

            var isLoader = info.Id.Equals(game.LoaderPackage, StringComparison.OrdinalIgnoreCase);
            using var zip = ZipFile.OpenRead(zipPath);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            var files = isLoader ? PlanLoader(names, game.LoaderRootFolder) : PlanPackage(names, info.Id);
            // A pure modpack may hold only its manifest and readme; those are still written to its folder.
            if (files.Count == 0) throw new InvalidOperationException($"{info.Id} contains nothing that can be installed.");
            plans.Add((todo[i], zipPath, files, isLoader));
        }

        // Loader first.
        var done = 0;
        foreach (var plan in plans.OrderByDescending(p => p.IsLoader))
        {
                var info = plan.Wanted.Info;
                progress?.Report((80 + done++ * 20 / plans.Count, $"Installing {info.Id} {info.Version}..."));
                installed.TryGetValue(info.Id, out var old);

                var mod = new ThunderstoreMod
                {
                    Id = info.Id,
                    Version = info.Version,
                    IsLoader = plan.IsLoader,
                    Requested = plan.Wanted.Requested || old?.Requested == true,
                    // An update keeps a disabled mod disabled.
                    Enabled = plan.IsLoader || (old?.Enabled ?? plan.Wanted.Enabled),
                    Dependencies = info.Dependencies.Select(DependencyId).ToList(),
                };
                if (old != null && !plan.IsLoader) RemoveFiles(root, old);
                installed[mod.Id] = mod;
                try
                {
                    using var zip = ZipFile.OpenRead(plan.Zip);
                    var keptConfigs = 0;
                    foreach (var placed in plan.Files)
                    {
                        var target = FullPath(root, placed.Target);
                        if (placed.IsConfig && File.Exists(target)) { keptConfigs++; continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        var entry = zip.Entries.First(e => e.FullName == placed.Entry);
                        entry.ExtractToFile(target + (mod.Enabled || placed.IsConfig ? "" : ".old"), overwrite: true);
                        if (!placed.IsConfig) mod.Files.Add(placed.Target);
                    }
                    log.Add(old == null ? $"Installed {mod.Id} {mod.Version}." : $"Updated {mod.Id} {old.Version} → {mod.Version}.");
                    if (keptConfigs > 0) log.Add($"Kept {keptConfigs} existing config file(s) from {mod.Id}.");
                    if (!plan.IsLoader) WarnAboutDuplicates(root, mod, log);
                }
                finally { SaveState(root, installed); }
        }
        SaveState(root, installed);
    }

    // A copy of the same plugin placed by hand (or copied over from r2modman) would be loaded twice.
    private static void WarnAboutDuplicates(string root, ThunderstoreMod mod, List<string> log)
    {
        var plugins = Path.Combine(root, "BepInEx", "plugins");
        if (!Directory.Exists(plugins)) return;
        var own = mod.Files.Select(f => FullPath(root, f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = own.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modFolder = Path.Combine(plugins, mod.Id) + Path.DirectorySeparatorChar;
        foreach (var other in Directory.EnumerateFiles(plugins, "*.dll", SearchOption.AllDirectories))
            if (names.Contains(Path.GetFileName(other)) && !own.Contains(other) && !other.StartsWith(modFolder, StringComparison.OrdinalIgnoreCase))
                log.Add($"Warning: {Path.GetFileName(other)} also exists at {Path.GetRelativePath(root, other)} — remove that copy, or the mod loads twice.");
    }

    // ── r2modman profile code ────────────────────────────────────────────────

    public sealed record ProfileEntry(string Id, string Version, bool Enabled);

    private static readonly string[] ProfileConfigExtensions =
        [".cfg", ".json", ".yml", ".yaml", ".txt", ".xml", ".ini", ".toml", ".csv", ".png", ".jpg"];

    private async Task InstallProfileAsync(string root, ThunderstoreGame game, Dictionary<string, ThunderstoreMod> installed,
        string code, List<string> log, IProgress<(int pct, string msg)>? progress)
    {
        string payload;
        using (var resp = await GetAsync($"{Api}/api/experimental/legacyprofile/get/{code}/",
                   "Profile code not found. It may have expired — export a new one in r2modman."))
            payload = await resp.Content.ReadAsStringAsync();
        const string prefix = "#r2modman";
        if (!payload.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("That code is not an r2modman profile.");

        using var archive = new ZipArchive(new MemoryStream(Convert.FromBase64String(payload[prefix.Length..].Trim())));
        var manifest = archive.GetEntry("export.r2x") ?? throw new InvalidOperationException("The profile has no mod list.");
        string yaml;
        using (var reader = new StreamReader(manifest.Open(), Encoding.UTF8)) yaml = await reader.ReadToEndAsync();
        var entries = ParseProfile(yaml);
        if (entries.Count == 0) throw new InvalidOperationException("The profile contains no mods.");

        // A profile pins exact versions, so the server matches the players.
        var wanted = new Dictionary<string, Wanted>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            progress?.Report((i * 50 / entries.Count, $"Downloading {e.Id} ({i + 1}/{entries.Count})..."));
            var request = Checked(e.Id[..e.Id.LastIndexOf('-')], e.Id[(e.Id.LastIndexOf('-') + 1)..], e.Version, null);
            if (installed.TryGetValue(e.Id, out var have) && have.Version == e.Version && IsPresent(root, have))
            {
                have.Requested = true;
                if (have.Enabled != e.Enabled && !have.IsLoader) SetEnabled(root, have, e.Enabled);
                continue;
            }
            try
            {
                var info = await FetchExactAsync(root, request.Namespace!, request.Name!, request.Version!);
                wanted[info.Id] = new Wanted { Info = info, Requested = true, Enabled = e.Enabled };
            }
            catch (InvalidOperationException ex) { missing.Add(ex.Message); }
        }
        if (missing.Count > 0)
            throw new InvalidOperationException("Nothing was installed. " + string.Join(" ", missing.Take(5)));

        await ApplyAsync(root, game, installed, wanted, log, progress);
        SaveState(root, installed);

        // Only data files under config/ are taken from the profile — never code.
        int written = 0, kept = 0, skipped = 0;
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (path.EndsWith('/') || path is "export.r2x" or "mods.yml") continue;
            if (path.StartsWith("config/", StringComparison.OrdinalIgnoreCase)) path = "BepInEx/" + path;
            if (!path.StartsWith(ConfigRoute + "/", StringComparison.OrdinalIgnoreCase) || path.Split('/').Any(p => p is ".." or "." || p.Contains(':'))
                || !ProfileConfigExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }
            var target = FullPath(root, path);
            if (File.Exists(target)) { kept++; continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target);
            written++;
        }
        log.Add($"Profile: {entries.Count} mod(s). Config files added: {written}"
            + (kept > 0 ? $", existing kept: {kept}" : "") + (skipped > 0 ? $", other files ignored: {skipped}" : "") + ".");
    }

    /// <summary>Reads the mod list of r2modman's export.r2x (YAML with a fixed, simple shape).</summary>
    public static List<ProfileEntry> ParseProfile(string yaml)
    {
        var result = new List<ProfileEntry>();
        string? name = null;
        int major = 0, minor = 0, patch = 0;
        var enabled = true;
        void Flush()
        {
            if (name != null && name.Contains('-')) result.Add(new ProfileEntry(name, $"{major}.{minor}.{patch}", enabled));
            name = null; major = minor = patch = 0; enabled = true;
        }
        var inMods = false;
        foreach (var raw in yaml.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            if (!char.IsWhiteSpace(line[0]) && line[0] != '-') { Flush(); inMods = line.StartsWith("mods:"); continue; }
            if (!inMods) continue;
            var text = line.Trim();
            if (text.StartsWith("- ")) { Flush(); text = text[2..].Trim(); }
            var colon = text.IndexOf(':');
            if (colon < 0) continue;
            var key = text[..colon].Trim();
            var value = text[(colon + 1)..].Trim().Trim('"', '\'');
            switch (key)
            {
                case "name": name = value; break;
                case "major": int.TryParse(value, out major); break;
                case "minor": int.TryParse(value, out minor); break;
                case "patch": int.TryParse(value, out patch); break;
                case "enabled": enabled = !value.Equals("false", StringComparison.OrdinalIgnoreCase); break;
            }
        }
        Flush();
        return result;
    }

    // ── Uninstall / enable / disable / update ────────────────────────────────

    private static void RemoveFiles(string root, ThunderstoreMod mod)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in mod.Files)
        {
            var full = FullPath(root, relative);
            foreach (var path in new[] { full, full + ".old" })
                if (File.Exists(path)) File.Delete(path);
            // Remember every folder up to (not including) BepInEx\<kind> so empty ones can be removed.
            for (var dir = Path.GetDirectoryName(full)!; Path.GetRelativePath(root, dir).Split(Path.DirectorySeparatorChar).Length > 2; dir = Path.GetDirectoryName(dir)!)
                folders.Add(dir);
        }
        foreach (var dir in folders.OrderByDescending(d => d.Length))
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
    }

    private static void SetEnabled(string root, ThunderstoreMod mod, bool enabled)
    {
        foreach (var relative in mod.Files)
        {
            var on = FullPath(root, relative);
            var off = on + ".old";
            var (from, to) = enabled ? (off, on) : (on, off);
            if (File.Exists(from)) File.Move(from, to, overwrite: true);
        }
        mod.Enabled = enabled;
    }

    public Task<List<string>> UninstallAsync(GameServer server, string id) => LockedAsync(server, root =>
    {
        var installed = LoadState(server);
        if (!installed.TryGetValue(id, out var mod)) throw new InvalidOperationException($"{id} is not installed by WGS.");

        var dependents = installed.Values.Where(m => m.Id != mod.Id
            && (mod.IsLoader || m.Dependencies.Contains(mod.Id, StringComparer.OrdinalIgnoreCase))).Select(m => m.Id).ToList();
        if (dependents.Count > 0)
            throw new InvalidOperationException($"{mod.Id} is needed by: {string.Join(", ", dependents.Take(8))}{(dependents.Count > 8 ? ", ..." : "")}. Remove those first.");

        var log = new List<string>();
        void Remove(ThunderstoreMod m)
        {
            RemoveFiles(root, m);
            installed.Remove(m.Id);
            log.Add($"Removed {m.Id}.");
        }
        try
        {
            Remove(mod);
            // Dependencies nobody asked for and nothing else needs go too (the loader stays).
            while (installed.Values.FirstOrDefault(m => !m.Requested && !m.IsLoader
                       && !installed.Values.Any(o => o.Dependencies.Contains(m.Id, StringComparer.OrdinalIgnoreCase))) is { } orphan)
                Remove(orphan);
        }
        finally { SaveState(root, installed); }
        log.Add("Config files were left in BepInEx\\config.");
        return Task.FromResult(log);
    });

    public Task<List<string>> SetModEnabledAsync(GameServer server, string id, bool enabled) => LockedAsync(server, root =>
    {
        var installed = LoadState(server);
        if (!installed.TryGetValue(id, out var mod)) throw new InvalidOperationException($"{id} is not installed by WGS.");
        if (mod.IsLoader) throw new InvalidOperationException("The mod loader can't be disabled.");

        var log = new List<string>();
        try
        {
            if (!enabled)
            {
                var users = installed.Values.Where(m => m.Enabled && m.Dependencies.Contains(mod.Id, StringComparer.OrdinalIgnoreCase)).Select(m => m.Id).ToList();
                if (users.Count > 0) throw new InvalidOperationException($"{mod.Id} is needed by: {string.Join(", ", users.Take(8))}. Disable those first.");
                SetEnabled(root, mod, false);
                log.Add($"Disabled {mod.Id}.");
            }
            else
            {
                var queue = new Queue<ThunderstoreMod>([mod]);
                while (queue.Count > 0)
                {
                    var m = queue.Dequeue();
                    if (!m.Enabled) { SetEnabled(root, m, true); log.Add($"Enabled {m.Id}."); }
                    foreach (var dep in m.Dependencies)
                        if (installed.TryGetValue(dep, out var d) && !d.Enabled) queue.Enqueue(d);
                }
            }
        }
        finally { SaveState(root, installed); }
        return Task.FromResult(log);
    });

    /// <summary>Installed packages with LatestVersion filled in.</summary>
    public async Task<List<ThunderstoreMod>> CheckUpdatesAsync(GameServer server)
    {
        var mods = GetInstalled(server);
        foreach (var mod in mods)
        {
            var dash = mod.Id.LastIndexOf('-');
            try { mod.LatestVersion = (await GetPackageAsync(mod.Id[..dash], mod.Id[(dash + 1)..])).Latest.Version; }
            catch (InvalidOperationException) { mod.LatestVersion = null; }   // removed from Thunderstore
        }
        return mods;
    }

    /// <summary>Newest version of every installed package, plus any dependency a new version adds.</summary>
    public Task<List<string>> UpdateAllAsync(GameServer server, ThunderstoreGame game, IProgress<(int pct, string msg)>? progress = null)
        => LockedAsync(server, async root =>
        {
            var log = new List<string>();
            var installed = LoadState(server);
            var wanted = new Dictionary<string, Wanted>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<VersionInfo>();

            foreach (var mod in installed.Values.ToList())
            {
                progress?.Report((0, $"Checking {mod.Id}..."));
                var dash = mod.Id.LastIndexOf('-');
                VersionInfo latest;
                try { latest = (await GetPackageAsync(mod.Id[..dash], mod.Id[(dash + 1)..])).Latest; }
                catch (InvalidOperationException ex) { log.Add($"Skipped {mod.Id}: {ex.Message}"); continue; }
                if (latest.Version == mod.Version) continue;
                wanted[latest.Id] = new Wanted { Info = latest };
                queue.Enqueue(latest);
            }
            while (queue.Count > 0)
            {
                foreach (var dependency in queue.Dequeue().Dependencies)
                {
                    var (ns, name, _) = SplitDependency(dependency);
                    var id = $"{ns}-{name}";
                    if (wanted.ContainsKey(id) || installed.ContainsKey(id)) continue;
                    if (id.Equals(game.LoaderPackage, StringComparison.OrdinalIgnoreCase) && HasUnmanagedLoader(root)) continue;
                    var info = (await GetPackageAsync(ns, name)).Latest;
                    wanted[id] = new Wanted { Info = info };
                    queue.Enqueue(info);
                }
            }
            if (wanted.Count == 0) log.Add("Everything is up to date.");
            else await ApplyAsync(root, game, installed, wanted, log, progress);
            return log;
        });
}
