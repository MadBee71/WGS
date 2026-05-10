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

public class ConfigEditorService
{
    private static readonly string[] ConfigExtensions = [".cfg", ".ini", ".json", ".yaml", ".yml", ".toml", ".properties", ".conf", ".txt"];
    private static readonly string[] ConfigNames       = ["server", "config", "settings", "game", "serverconfig", "server_config"];

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

    public void Save(ConfigFileEntry entry)
        => File.WriteAllText(entry.Path, entry.Content, System.Text.Encoding.UTF8);

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path, System.Text.Encoding.UTF8); }
        catch { return string.Empty; }
    }
}
