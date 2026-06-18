using System.IO;
using WGS.Models;

namespace WGS.Services;

public class JunkItem
{
    public string Path        { get; set; } = string.Empty;
    public bool   IsDirectory { get; set; }
    public long   SizeBytes   { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SizeText => SizeBytes > 1_000_000
        ? $"{SizeBytes / 1_000_000.0:F1} MB"
        : $"{SizeBytes / 1_000.0:F0} KB";
}

/// <summary>
/// Finds leftover junk in a server's install directory that's safe to delete while the
/// server is stopped: old log files, Unreal Engine crash report folders, and stray SteamCMD
/// temp files. Never runs against a running server's process — only scans the filesystem.
/// </summary>
public class ServerHygieneService
{
    public List<JunkItem> ScanJunk(GameServer server)
    {
        var items = new List<JunkItem>();
        if (!Directory.Exists(server.InstallPath)) return items;

        try
        {
            // Old log files
            foreach (var f in Directory.EnumerateFiles(server.InstallPath, "*.log", SearchOption.AllDirectories))
            {
                if (IsInModFolder(f)) continue;
                var fi = new FileInfo(f);
                items.Add(new JunkItem { Path = f, SizeBytes = fi.Length, Description = "Log file" });
            }

            // Unreal Engine crash report folders (UECC-Windows-...)
            foreach (var d in Directory.EnumerateDirectories(server.InstallPath, "UECC-*", SearchOption.AllDirectories))
            {
                items.Add(new JunkItem { Path = d, IsDirectory = true, SizeBytes = DirSize(d), Description = "Crash report folder" });
            }

            // Stray crash dumps
            foreach (var f in Directory.EnumerateFiles(server.InstallPath, "*.dmp", SearchOption.AllDirectories))
            {
                if (IsInModFolder(f)) continue;
                var fi = new FileInfo(f);
                items.Add(new JunkItem { Path = f, SizeBytes = fi.Length, Description = "Crash dump" });
            }

            // Stray SteamCMD temp files left behind by an interrupted update
            foreach (var f in Directory.EnumerateFiles(server.InstallPath, "*.tmp", SearchOption.AllDirectories))
            {
                if (IsInModFolder(f)) continue;
                var fi = new FileInfo(f);
                items.Add(new JunkItem { Path = f, SizeBytes = fi.Length, Description = "Leftover temp file" });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        return items;
    }

    // Known mod-loader folder names whose own logs/temp files should never be flagged as junk —
    // BepInEx (Valheim, Subnautica, etc.), Melon/MelonLoader, SML2 (Satisfactory) all write
    // their own *.log files that would otherwise be wrongly caught by the patterns above.
    private static readonly string[] ModFolderNames = ["BepInEx", "MelonLoader", "SML2"];

    private static bool IsInModFolder(string filePath)
    {
        var dir = System.IO.Path.GetDirectoryName(filePath) ?? "";
        return ModFolderNames.Any(name =>
            dir.Contains($"{System.IO.Path.DirectorySeparatorChar}{name}", StringComparison.OrdinalIgnoreCase));
    }

    public void DeleteJunk(IEnumerable<JunkItem> items)
    {
        foreach (var item in items)
        {
            try
            {
                if (item.IsDirectory) Directory.Delete(item.Path, recursive: true);
                else File.Delete(item.Path);
            }
            catch { }
        }
    }

    private static long DirSize(string path)
    {
        try { return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length); }
        catch { return 0; }
    }
}
