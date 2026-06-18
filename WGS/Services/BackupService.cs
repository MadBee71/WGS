using System.IO;
using System.IO.Compression;
using WGS.Models;

namespace WGS.Services;

public class BackupEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public string SizeText => SizeBytes > 1_000_000
        ? $"{SizeBytes / 1_000_000.0:F1} MB"
        : $"{SizeBytes / 1_000.0:F0} KB";
}

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

    public async Task<BackupEntry> CreateBackupAsync(GameServer server)
    {
        if (!Directory.Exists(server.InstallPath))
            throw new DirectoryNotFoundException("Install directory not found: " + server.InstallPath);

        var safeName = string.Join("_", server.DisplayName.Split(Path.GetInvalidFileNameChars()));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outDir  = Path.Combine(BackupRoot, server.Id);
        Directory.CreateDirectory(outDir);

        var zipPath = Path.Combine(outDir, $"{safeName}_{timestamp}.zip");
        ProgressMessage?.Invoke($"[Backup] Compressing {server.DisplayName}...");

        try
        {
            await Task.Run(() =>
            {
                // exclude large executables, only back up config/save data
                var saveDirs = GetSaveDirectories(server);
                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                foreach (var dir in saveDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(server.InstallPath, file);
                        zip.CreateEntryFromFile(file, rel, CompressionLevel.Optimal);
                    }
                }
            });
        }
        catch
        {
            try { File.Delete(zipPath); } catch { }
            throw;
        }

        var info  = new FileInfo(zipPath);
        ProgressMessage?.Invoke($"[Backup] Done — {new BackupEntry { SizeBytes = info.Length }.SizeText}");

        var entry = new BackupEntry
        {
            FilePath    = zipPath,
            ServerName  = server.DisplayName,
            CreatedAt   = DateTime.Now,
            SizeBytes   = info.Length,
        };

        ApplyRetentionPolicy(server, server.BackupRetention);
        return entry;
    }

    public void ApplyRetentionPolicy(GameServer server, int maxKeep)
    {
        foreach (var old in GetBackupsToDelete(server, maxKeep))
            DeleteBackup(old.FilePath);
    }

    /// <summary>Backups that would be removed by the current retention policy (count + max age), without deleting them.</summary>
    public List<BackupEntry> GetBackupsToDelete(GameServer server, int maxKeep)
    {
        var backups = GetBackupsForServer(server);
        var toDelete = new List<BackupEntry>();

        if (maxKeep > 0)
            toDelete.AddRange(backups.Skip(maxKeep));

        if (server.BackupMaxAgeDays > 0)
        {
            var cutoff = DateTime.Now.AddDays(-server.BackupMaxAgeDays);
            toDelete.AddRange(backups.Where(b => b.CreatedAt < cutoff && !toDelete.Contains(b)));
        }

        return toDelete;
    }

    public async Task RestoreBackupAsync(GameServer server, string zipPath)
    {
        ProgressMessage?.Invoke($"[Restore] Extracting backup...");
        await Task.Run(() =>
            ZipFile.ExtractToDirectory(zipPath, server.InstallPath, overwriteFiles: true));
        ProgressMessage?.Invoke($"[Restore] Done.");
    }

    public List<BackupEntry> GetBackupsForServer(GameServer server)
    {
        var dir = Path.Combine(BackupRoot, server.Id);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.zip")
            .Select(f => { var i = new FileInfo(f); return new BackupEntry
            {
                FilePath   = f,
                ServerName = server.DisplayName,
                CreatedAt  = i.CreationTime,
                SizeBytes  = i.Length,
            }; })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public void DeleteBackup(string path)
    {
        if (File.Exists(path)) File.Delete(path);
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
