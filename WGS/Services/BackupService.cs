using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

public class BackupEntry
{
    public string FilePath      { get; set; } = string.Empty;
    public string ServerName    { get; set; } = string.Empty;
    public DateTime CreatedAt   { get; set; }
    public long SizeBytes       { get; set; }
    public bool IsIncremental   { get; set; }
    /// <summary>Full path of the backup this one's diff is relative to. Empty for full backups.</summary>
    public string BaseFilePath  { get; set; } = string.Empty;
    /// <summary>Favorited backups are exempt from automatic retention/age cleanup — the user has
    /// to delete them explicitly. Community-requested (SkOODaT, Discord): "pin" a backup before a
    /// major update so it never gets swept away by retention limits.</summary>
    public bool IsFavorite      { get; set; }
    public string SizeText => SizeBytes > 1_000_000
        ? $"{SizeBytes / 1_000_000.0:F1} MB"
        : $"{SizeBytes / 1_000.0:F0} KB";
}

internal record FileState(string RelPath, long Length, long WriteTicks);
internal record BackupSidecar(bool IsIncremental, string BaseFileName, List<FileState> Files);

public class BackupService
{
    private readonly ConfigService _config;
    public string BackupRoot => _config.BackupPath;

    public BackupService(ConfigService config)
    {
        _config    = config;
        Directory.CreateDirectory(config.BackupPath);
    }

    private const string DeletedMarkerEntry = "__deleted.txt";

    // Some games keep their saves in the Windows user profile instead of the install folder. Those files are stored
    // under this prefix and restored to the profile of whoever runs the restore, i.e. the account that runs the server.
    private const string LocalAppDataPrefix = "__localappdata__";
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static bool IsUnder(string file, string folder) =>
        folder.Length > 0 && Path.GetFullPath(file).StartsWith(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalAppDataEntry(string entry) =>
        entry.StartsWith(LocalAppDataPrefix + "\\", StringComparison.Ordinal) || entry.StartsWith(LocalAppDataPrefix + "/", StringComparison.Ordinal);

    private static string ToEntryName(GameServer server, string file) =>
        !IsUnder(file, server.InstallPath) && IsUnder(file, LocalAppData)
            ? Path.Combine(LocalAppDataPrefix, Path.GetRelativePath(LocalAppData, file))
            : Path.GetRelativePath(server.InstallPath, file);

    private static string ToFullPath(GameServer server, string entry) =>
        IsLocalAppDataEntry(entry)
            ? Path.Combine(LocalAppData, entry[(LocalAppDataPrefix.Length + 1)..])
            : Path.Combine(server.InstallPath, entry);

    public async Task<BackupEntry> CreateBackupAsync(GameServer server)
    {
        if (!Directory.Exists(server.InstallPath))
            throw new DirectoryNotFoundException("Install directory not found: " + server.InstallPath);

        var safeName  = string.Join("_", server.DisplayName.Split(Path.GetInvalidFileNameChars()));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outDir    = Path.Combine(BackupRoot, server.Id);
        Directory.CreateDirectory(outDir);
        var zipPath = Path.Combine(outDir, $"{safeName}_{timestamp}.zip");

        // Snapshot current file state for every resolved backup entry (directories, single
        // files, and wildcard patterns are all supported — see ResolveBackupFiles).
        var current = new Dictionary<string, FileState>();
        foreach (var file in ResolveBackupFiles(server))
        {
            var rel = ToEntryName(server, file);
            var fi  = new FileInfo(file);
            current[rel] = new FileState(rel, fi.Length, fi.LastWriteTimeUtc.Ticks);
        }

        // Decide full vs incremental
        var existing = GetBackupsForServer(server); // newest first
        var previous = existing.FirstOrDefault();
        int sinceFull = 0;
        bool hasFullAncestor = false;
        foreach (var b in existing)
        {
            if (!b.IsIncremental) { hasFullAncestor = true; break; }
            sinceFull++;
        }
        var fullEveryN = Math.Max(1, server.FullBackupEveryN);
        bool makeFull = !server.UseIncrementalBackups || previous == null || !hasFullAncestor || sinceFull >= fullEveryN - 1;

        Dictionary<string, FileState> prevFiles = new();
        if (!makeFull && previous != null)
            prevFiles = LoadSidecar(previous.FilePath)?.Files.ToDictionary(f => f.RelPath) ?? new();

        // The first backup that reaches outside the install folder starts a new chain: continuing an older one would
        // list everything that is no longer backed up as "deleted", and a restore would then remove those files.
        if (!makeFull && current.Keys.Any(IsLocalAppDataEntry) && !prevFiles.Keys.Any(IsLocalAppDataEntry))
        {
            makeFull = true;
            prevFiles = new();
        }

        var toInclude = makeFull
            ? current.Keys.ToList()
            : current.Where(kv => !prevFiles.TryGetValue(kv.Key, out var old)
                                   || old.Length != kv.Value.Length || old.WriteTicks != kv.Value.WriteTicks)
                     .Select(kv => kv.Key).ToList();
        var deleted = makeFull ? [] : prevFiles.Keys.Where(k => !current.ContainsKey(k)).ToList();

        try
        {
            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                foreach (var rel in toInclude)
                {
                    var full = ToFullPath(server, rel);
                    if (File.Exists(full)) zip.CreateEntryFromFile(full, rel, CompressionLevel.Optimal);
                }
                if (deleted.Count > 0)
                {
                    var entry = zip.CreateEntry(DeletedMarkerEntry);
                    using var s = entry.Open();
                    using var w = new StreamWriter(s);
                    foreach (var d in deleted) w.WriteLine(d);
                }
            });
        }
        catch
        {
            try { File.Delete(zipPath); } catch { }
            throw;
        }

        SaveSidecar(zipPath, new BackupSidecar(!makeFull, makeFull ? "" : previous!.FilePath, current.Values.ToList()));

        var info = new FileInfo(zipPath);

        var entry = new BackupEntry
        {
            FilePath      = zipPath,
            ServerName    = server.DisplayName,
            CreatedAt     = DateTime.Now,
            SizeBytes     = info.Length,
            IsIncremental = !makeFull,
            BaseFilePath  = makeFull ? "" : previous!.FilePath,
        };

        ApplyRetentionPolicy(server, server.BackupRetention);
        return entry;
    }

    public void ApplyRetentionPolicy(GameServer server, int maxKeep)
    {
        foreach (var old in GetBackupsToDelete(server, maxKeep))
            DeleteBackup(old.FilePath);
    }

    /// <summary>Backups that would be removed by the current retention policy (count + max age), without deleting them.
    /// Never includes a backup that a newer, kept backup still depends on via its incremental chain.</summary>
    public List<BackupEntry> GetBackupsToDelete(GameServer server, int maxKeep)
    {
        var backups = GetBackupsForServer(server); // newest first
        var candidates = new List<BackupEntry>();

        if (maxKeep > 0)
            candidates.AddRange(backups.Skip(maxKeep));

        if (server.BackupMaxAgeDays > 0)
        {
            var cutoff = DateTime.Now.AddDays(-server.BackupMaxAgeDays);
            candidates.AddRange(backups.Where(b => b.CreatedAt < cutoff && !candidates.Contains(b)));
        }

        // Favorited backups are never auto-cleaned, regardless of retention/age settings.
        candidates.RemoveAll(b => b.IsFavorite);

        if (candidates.Count == 0) return candidates;

        // Protect any backup that a kept (non-candidate) backup depends on, walking its chain.
        var keepFilePaths = backups.Select(b => b.FilePath).Except(candidates.Select(c => c.FilePath)).ToHashSet();
        var byPath = backups.ToDictionary(b => b.FilePath);
        var protected_ = new HashSet<string>();
        foreach (var keptPath in keepFilePaths)
        {
            var b = byPath[keptPath];
            while (b.IsIncremental && !string.IsNullOrEmpty(b.BaseFilePath) && byPath.TryGetValue(b.BaseFilePath, out var baseEntry))
            {
                protected_.Add(baseEntry.FilePath);
                b = baseEntry;
            }
        }

        return candidates.Where(c => !protected_.Contains(c.FilePath)).ToList();
    }

    public async Task RestoreBackupAsync(GameServer server, string zipPath)
    {
        var all = GetBackupsForServer(server).ToDictionary(b => b.FilePath);
        if (!all.TryGetValue(zipPath, out var target))
            target = new BackupEntry { FilePath = zipPath, IsIncremental = false };

        // Walk the chain back to the nearest full backup, then restore oldest → newest
        var chain = new List<BackupEntry> { target };
        var cursor = target;
        while (cursor.IsIncremental && !string.IsNullOrEmpty(cursor.BaseFilePath) && all.TryGetValue(cursor.BaseFilePath, out var baseEntry))
        {
            chain.Add(baseEntry);
            cursor = baseEntry;
        }
        chain.Reverse();

        await Task.Run(() =>
        {
            var restored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Satisfactory: nothing of the current saves is lost by a restore, replaced files are kept aside
            var aside = server.GameId == "satisfactory" ? SatisfactoryPlugin.NewReplacedSavesFolder() : null;
            void KeepCurrent(string path)
            {
                if (aside != null && !restored.Contains(Path.GetFullPath(path)))
                    SatisfactoryPlugin.KeepReplacedSave(path, aside);
            }
            foreach (var entry in chain)
            {
                using var zip = ZipFile.OpenRead(entry.FilePath);
                var deletedList = new List<string>();
                foreach (var zipEntry in zip.Entries)
                {
                    if (zipEntry.FullName == DeletedMarkerEntry)
                    {
                        using var s = zipEntry.Open();
                        using var r = new StreamReader(s);
                        string? line;
                        while ((line = r.ReadLine()) != null)
                            if (!string.IsNullOrWhiteSpace(line)) deletedList.Add(line.Trim());
                        continue;
                    }

                    var dest = ToFullPath(server, zipEntry.FullName);
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                    KeepCurrent(dest);
                    zipEntry.ExtractToFile(dest, overwrite: true);
                    restored.Add(Path.GetFullPath(dest));
                }

                foreach (var rel in deletedList)
                {
                    try { var p = ToFullPath(server, rel); KeepCurrent(p); restored.Remove(Path.GetFullPath(p)); if (File.Exists(p)) File.Delete(p); }
                    catch { }
                }
            }

            // Satisfactory loads the newest save of a world, so saves made after the backup must be moved out of the way.
            if (aside != null)
                SatisfactoryPlugin.SetAsideOtherSaves(restored, aside);
        });
    }

    public List<BackupEntry> GetBackupsForServer(GameServer server)
    {
        var dir = Path.Combine(BackupRoot, server.Id);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.zip")
            .Select(f =>
            {
                var i = new FileInfo(f);
                var sidecar = LoadSidecar(f);
                return new BackupEntry
                {
                    FilePath      = f,
                    ServerName    = server.DisplayName,
                    CreatedAt     = i.CreationTime,
                    SizeBytes     = i.Length,
                    IsIncremental = sidecar?.IsIncremental ?? false,
                    BaseFilePath  = sidecar?.BaseFileName ?? "",
                    IsFavorite    = File.Exists(FavoritePath(f)),
                };
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public void DeleteBackup(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        try { File.Delete(SidecarPath(path)); } catch { }
        try { File.Delete(FavoritePath(path)); } catch { }
    }

    /// <summary>Marks (or unmarks) a backup as a favorite — favorited backups are excluded from
    /// GetBackupsToDelete regardless of retention/age settings, but can still be deleted manually.</summary>
    public void SetFavorite(string zipPath, bool favorite)
    {
        var path = FavoritePath(zipPath);
        if (favorite) File.WriteAllText(path, "");
        else try { File.Delete(path); } catch { }
    }

    private static string SidecarPath(string zipPath)  => zipPath + ".meta.json";
    private static string FavoritePath(string zipPath) => zipPath + ".favorite";

    private static void SaveSidecar(string zipPath, BackupSidecar sidecar)
    {
        try { File.WriteAllText(SidecarPath(zipPath), JsonConvert.SerializeObject(sidecar)); }
        catch { }
    }

    private static BackupSidecar? LoadSidecar(string zipPath)
    {
        var path = SidecarPath(zipPath);
        if (!File.Exists(path)) return null;
        try { return JsonConvert.DeserializeObject<BackupSidecar>(File.ReadAllText(path)); }
        catch { return null; }
    }

    /// <summary>
    /// Resolves each raw entry from GetSaveDirectories into actual files to back up. An entry
    /// can be:
    ///   - a directory (existing behavior — backed up recursively, every file under it)
    ///   - a single file (e.g. a root-level config file that isn't inside its own directory)
    ///   - a wildcard pattern (e.g. "*.cfg", "configs\*.json") — matched non-recursively against
    ///     files directly in the pattern's parent folder
    /// Entries that don't exist on disk are silently skipped, same as directories always were.
    /// Community-requested (Shmightworks, Discord): lets a custom BackupSavePath pinpoint just
    /// the files that matter instead of always requiring a whole directory.
    /// </summary>
    private static IEnumerable<string> ResolveBackupFiles(GameServer server)
    {
        foreach (var spec in GetSaveDirectories(server))
        {
            if (spec.IndexOfAny(['*', '?']) >= 0)
            {
                var dir     = Path.GetDirectoryName(spec);
                var pattern = Path.GetFileName(spec);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(pattern) || !Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                    yield return file;
            }
            else if (File.Exists(spec))
            {
                yield return spec;
            }
            else if (Directory.Exists(spec))
            {
                foreach (var file in Directory.EnumerateFiles(spec, "*", SearchOption.AllDirectories))
                    yield return file;
            }
            // else: doesn't exist on disk — skipped, same as a missing directory always was
        }
    }

    private static string[] GetSaveDirectories(GameServer server)
    {
        // Per-server custom path takes priority over game defaults
        if (!string.IsNullOrWhiteSpace(server.BackupSavePath))
        {
            return server.BackupSavePath
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => Path.IsPathRooted(p) ? p : Path.Combine(server.InstallPath, p))
                .ToArray();
        }

        return server.GameId switch
        {
            "valheim"          => [Path.Combine(server.InstallPath, "saves")],
            // "Minecraft Java" can run Paper (Server type), which keeps the Nether and End in their own
            // folders next to "world" — they were missing from backups. Missing folders are skipped,
            // so vanilla servers (everything inside "world") are unaffected.
            "minecraft"        => [Path.Combine(server.InstallPath, "world"), Path.Combine(server.InstallPath, "world_nether"),
                                   Path.Combine(server.InstallPath, "world_the_end"), Path.Combine(server.InstallPath, "plugins")],
            // Paper keeps the Nether and End in their own folders next to "world" (missing ones are skipped).
            "minecraft_paper"  => [Path.Combine(server.InstallPath, "world"), Path.Combine(server.InstallPath, "world_nether"),
                                   Path.Combine(server.InstallPath, "world_the_end"), Path.Combine(server.InstallPath, "plugins")],
            "conanexiles"      => [Path.Combine(server.InstallPath, "ConanSandbox", "Saved")],
            "rust"             => [Path.Combine(server.InstallPath, "server")],
            "7daystodie"       => [Path.Combine(server.InstallPath, "Saves"), Path.Combine(server.InstallPath, "UserDataFolder")],
            "theforest"        => [Path.Combine(server.InstallPath, "saves")],
            "sonsoftheforest"  => [Path.Combine(server.InstallPath, "Saves")],
            "armareforger"     => [Path.Combine(server.InstallPath, "ArmaReforgerServer", "Worlds")],
            "enshrouded"       => [Path.Combine(server.InstallPath, "savegame")],
            "palworld"         => [Path.Combine(server.InstallPath, "Pal", "Saved", "SaveGames")],
            "ark"              => [Path.Combine(server.InstallPath, "ShooterGame", "Saved")],
            "arksa"            => [Path.Combine(server.InstallPath, "ShooterGame", "Saved")],
            "dayz"             => [Path.Combine(server.InstallPath, "mpmissions")],
            "vrising"          => [Path.Combine(server.InstallPath, "VRisingServer_Data", "StreamingAssets", "Settings"),
                                   Path.Combine(server.InstallPath, "save-data")],
            // Worlds are in the user profile, not in the install folder. Only the server's own folders: SaveGames also
            // holds the saves of someone who plays the game on this PC.
            "satisfactory"     => [Path.Combine(SatisfactoryPlugin.SaveGamesPath, "server"),
                                   Path.Combine(SatisfactoryPlugin.SaveGamesPath, "blueprints"),
                                   Path.Combine(SatisfactoryPlugin.SaveGamesPath, $"ServerSettings.{server.ServerPort}.sav"),
                                   Path.Combine(server.InstallPath, "FactoryGame", "Saved", "Config", "WindowsServer")],
            "zomboid"          => [Path.Combine(server.InstallPath, "Saves")],
            "terraria"         => [Path.Combine(server.InstallPath, "Worlds")],
            _                  => [server.InstallPath],
        };
    }
}
