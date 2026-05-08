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
    public string BackupRoot { get; }

    public event Action<string>? ProgressMessage;

    public BackupService(ConfigService config)
    {
        _config    = config;
        BackupRoot = Path.Combine(config.AppDataPath, "backups");
        Directory.CreateDirectory(BackupRoot);
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
        ProgressMessage?.Invoke($"[Backup] Pakataan {server.DisplayName}...");

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

        var info  = new FileInfo(zipPath);
        ProgressMessage?.Invoke($"[Backup] Valmis — {new BackupEntry { SizeBytes = info.Length }.SizeText}");

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
        if (maxKeep <= 0) return;
        foreach (var old in GetBackupsForServer(server).Skip(maxKeep))
            DeleteBackup(old.FilePath);
    }

    public async Task RestoreBackupAsync(GameServer server, string zipPath)
    {
        ProgressMessage?.Invoke($"[Restore] Puretaan varmuuskopio...");
        await Task.Run(() =>
            ZipFile.ExtractToDirectory(zipPath, server.InstallPath, overwriteFiles: true));
        ProgressMessage?.Invoke($"[Restore] Valmis.");
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

    private static string[] GetSaveDirectories(GameServer server) => server.GameId switch
    {
        "valheim"          => [Path.Combine(server.InstallPath, "saves")],
        "minecraft"        => [Path.Combine(server.InstallPath, "world"), Path.Combine(server.InstallPath, "plugins")],
        "conanexiles"      => [Path.Combine(server.InstallPath, "ConanSandbox", "Saved")],
        "rust"             => [Path.Combine(server.InstallPath, "server")],
        "7daystodie"       => [Path.Combine(server.InstallPath, "Saves"), Path.Combine(server.InstallPath, "UserDataFolder")],
        "theforest"        => [Path.Combine(server.InstallPath, "saves")],
        "sonsoftheforest"  => [Path.Combine(server.InstallPath, "Saves")],
        "armareforger"     => [Path.Combine(server.InstallPath, "ArmaReforgerServer", "Worlds")],
        _                  => [server.InstallPath],
    };
}
