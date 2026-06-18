using System.IO;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

public class ConfigFileEntry
{
    public string Name    { get; set; } = string.Empty;
    public string Path    { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ConfigSnapshot
{
    public string FilePath  { get; set; } = string.Empty; // path to the .bak snapshot file
    public DateTime SavedAt { get; set; }
}

public class ConfigEditorService
{
    private static readonly string[] ConfigExtensions = [".cfg", ".ini", ".json", ".yaml", ".yml", ".toml", ".properties", ".conf", ".txt"];
    private static readonly string[] ConfigNames       = ["server", "config", "settings", "game", "serverconfig", "server_config"];
    private readonly ConfigService _config;
    private const int MaxSnapshotsPerFile = 20;

    public ConfigEditorService(ConfigService config) => _config = config;

    public List<ConfigFileEntry> FindConfigs(GameServer server, IGamePlugin? plugin)
    {
        var result = new List<ConfigFileEntry>();
        if (!Directory.Exists(server.InstallPath)) return result;

        // Plugin-specified config files first
        if (plugin != null)
        {
            foreach (var rel in plugin.ConfigFiles)
            {
                var full = System.IO.Path.Combine(server.InstallPath, rel);
                if (File.Exists(full))
                    result.Add(new ConfigFileEntry
                    {
                        Name = System.IO.Path.GetFileName(full),
                        Path = full,
                    });
            }
        }

        // Auto-discover common config files
        foreach (var ext in ConfigExtensions)
        {
            try
            {
                foreach (var file in Directory.GetFiles(server.InstallPath, $"*{ext}", SearchOption.AllDirectories))
                {
                    if (result.Any(r => r.Path == file)) continue;
                    var name = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (ConfigNames.Any(n => name.Contains(n)))
                        result.Add(new ConfigFileEntry
                        {
                            Name = System.IO.Path.GetRelativePath(server.InstallPath, file),
                            Path = file,
                        });
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return result;
    }

    public void LoadContent(ConfigFileEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Content))
            entry.Content = SafeRead(entry.Path);
    }

    public void Save(string serverId, ConfigFileEntry entry)
    {
        SnapshotBeforeSave(serverId, entry.Path);
        File.WriteAllText(entry.Path, entry.Content, System.Text.Encoding.UTF8);
    }

    private void SnapshotBeforeSave(string serverId, string filePath)
    {
        try
        {
            var oldContent = SafeRead(filePath);
            if (oldContent.Length == 0) return; // nothing to snapshot (new/empty file)

            var dir = HistoryDir(serverId, filePath);
            Directory.CreateDirectory(dir);
            var snapPath = System.IO.Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            // Avoid clobbering a snapshot from a save that happened in the same second
            int suffix = 1;
            while (File.Exists(snapPath))
                snapPath = System.IO.Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{suffix++}.bak");
            File.WriteAllText(snapPath, oldContent, System.Text.Encoding.UTF8);

            // Cap history depth per file
            var snaps = Directory.GetFiles(dir, "*.bak").OrderByDescending(f => f).ToList();
            foreach (var stale in snaps.Skip(MaxSnapshotsPerFile))
                try { File.Delete(stale); } catch { }
        }
        catch { /* snapshotting must never block an actual save */ }
    }

    public List<ConfigSnapshot> GetHistory(string serverId, string filePath)
    {
        var dir = HistoryDir(serverId, filePath);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.bak")
            .Select(f => new ConfigSnapshot { FilePath = f, SavedAt = ParseTimestamp(f) })
            .OrderByDescending(s => s.SavedAt)
            .ToList();
    }

    public string ReadSnapshot(string snapshotPath) => SafeRead(snapshotPath);

    private static DateTime ParseTimestamp(string snapshotFilePath)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(snapshotFilePath);
        return DateTime.TryParseExact(name, "yyyyMMdd_HHmmss", null,
            System.Globalization.DateTimeStyles.None, out var dt) ? dt : File.GetCreationTime(snapshotFilePath);
    }

    private string HistoryDir(string serverId, string filePath)
    {
        var safeName = string.Join("_", System.IO.Path.GetFileName(filePath).Split(System.IO.Path.GetInvalidFileNameChars()));
        return System.IO.Path.Combine(_config.AppDataPath, "config_history", serverId, safeName);
    }

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path, System.Text.Encoding.UTF8); }
        catch { return string.Empty; }
    }
}
