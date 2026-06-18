using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
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

    public event Action<string>? ProgressMessage;

    public BackupService(ConfigService config)
    {
        _config    = config;
        Directory.CreateDirectory(config.BackupPath);
    }

    private const string DeletedMarkerEntry = "__deleted.txt";

    public async Task<BackupEntry> CreateBackupAsync(GameServer server)
    {
        if (!Directory.Exists(server.InstallPath))
            throw new DirectoryNotFoundException("Install directory not found: " + server.InstallPath);

        var safeName  = string.Join("_", server.DisplayName.Split(Path.GetInvalidFileNameChars()));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outDir    = Path.Combine(BackupRoot, server.Id);
        Directory.CreateDirectory(outDir);
        var zipPath = Path.Combine(outDir, $"{safeName}_{timestamp}.zip");

        // Snapshot current file state for every save directory
        var saveDirs = GetSaveDirectories(server);
        var current  = new Dictionary<string, FileState>();
        foreach (var dir in saveDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(server.InstallPath, file);
                var fi  = new FileInfo(file);
                current[rel] = new FileState(rel, fi.Length, fi.LastWriteTimeUtc.Ticks);
            }
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

        var toInclude = makeFull
            ? current.Keys.ToList()
            : current.Where(kv => !prevFiles.TryGetValue(kv.Key, out var old)
                                   || old.Length != kv.Value.Length || old.WriteTicks != kv.Value.WriteTicks)
                     .Select(kv => kv.Key).ToList();
        var deleted = makeFull ? [] : prevFiles.Keys.Where(k => !current.ContainsKey(k)).ToList();

        ProgressMessage?.Invoke($"[Backup] Compressing {server.DisplayName}{(makeFull ? "" : " (incremental)")}...");

        try
        {
            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                foreach (var rel in toInclude)
                {
                    var full = Path.Combine(server.InstallPath, rel);
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
        ProgressMessage?.Invoke($"[Backup] Done — {new BackupEntry { SizeBytes = info.Length }.SizeText}"
            + (makeFull ? "" : $" ({toInclude.Count} changed file(s))"));

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

        ProgressMessage?.Invoke(chain.Count > 1
            ? $"[Restore] Extracting {chain.Count} backups in chain (full + {chain.Count - 1} incremental)..."
            : "[Restore] Extracting backup...");

        await Task.Run(() =>
        {
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

                    var dest = Path.Combine(server.InstallPath, zipEntry.FullName);
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                    zipEntry.ExtractToFile(dest, overwrite: true);
                }

                foreach (var rel in deletedList)
                {
                    try { var p = Path.Combine(server.InstallPath, rel); if (File.Exists(p)) File.Delete(p); }
                    catch { }
                }
            }
        });

        ProgressMessage?.Invoke("[Restore] Done.");
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
                };
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public void DeleteBackup(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        try { File.Delete(SidecarPath(path)); } catch { }
    }

    private static string SidecarPath(string zipPath) => zipPath + ".meta.json";

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
            "minecraft"        => [Path.Combine(server.InstallPath, "world"), Path.Combine(server.InstallPath, "plugins")],
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
            "satisfactory"     => [Path.Combine(server.InstallPath, "FactoryGame", "Saved")],
            "zomboid"          => [Path.Combine(server.InstallPath, "Saves")],
            "terraria"         => [Path.Combine(server.InstallPath, "Worlds")],
            _                  => [server.InstallPath],
        };
    }
}
