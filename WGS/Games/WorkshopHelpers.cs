using System.IO;
using WGS.Services;

namespace WGS.Games;

/// <summary>
/// Group A — Arma/DayZ-style: symlink at server root + copy .bikey files.
/// Startup arg: -mod=@id1;@id2
/// </summary>
internal static class GroupAHelper
{
    public static Task OnModDownloadedAsync(string serverInstallPath, string workshopItemPath, ulong modId)
    {
        var linkPath = Path.Combine(serverInstallPath, $"@{modId}");
        SteamWorkshopService.CreateDirectoryLink(linkPath, workshopItemPath);

        // Copy .bikey files from keys/ or key/ subdirectory
        var keysDestDir = Path.Combine(serverInstallPath, "keys");
        Directory.CreateDirectory(keysDestDir);
        foreach (var keysSubDir in new[] { "keys", "key" })
        {
            var src = Path.Combine(workshopItemPath, keysSubDir);
            if (!Directory.Exists(src)) continue;
            foreach (var bikey in Directory.GetFiles(src, "*.bikey"))
            {
                var dest = Path.Combine(keysDestDir, Path.GetFileName(bikey));
                try { File.Copy(bikey, dest, overwrite: true); } catch { }
            }
        }
        return Task.CompletedTask;
    }

    public static Task OnModRemovedAsync(string serverInstallPath, string workshopItemPath, ulong modId)
    {
        SteamWorkshopService.RemoveDirectoryLink(Path.Combine(serverInstallPath, $"@{modId}"));

        // Remove .bikey files that came from this mod
        var keysDestDir = Path.Combine(serverInstallPath, "keys");
        foreach (var keysSubDir in new[] { "keys", "key" })
        {
            var src = Path.Combine(workshopItemPath, keysSubDir);
            if (!Directory.Exists(src)) continue;
            foreach (var bikey in Directory.GetFiles(src, "*.bikey"))
            {
                var dest = Path.Combine(keysDestDir, Path.GetFileName(bikey));
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
            }
        }
        return Task.CompletedTask;
    }

    public static string BuildModArguments(IReadOnlyList<ulong> activeModIds)
    {
        if (activeModIds.Count == 0) return string.Empty;
        return "-mod=" + string.Join(";", activeModIds.Select(id => $"@{id}"));
    }
}

/// <summary>
/// Group B — Mods subdirectory style: symlink in game's Mods/addons folder.
/// No startup arg needed (game scans folder automatically).
/// </summary>
internal static class GroupBHelper
{
    public static Task OnModDownloadedAsync(string serverInstallPath, string workshopItemPath,
        ulong modId, string modsSubDir)
    {
        var modsDir  = Path.Combine(serverInstallPath, modsSubDir);
        Directory.CreateDirectory(modsDir);
        var linkPath = Path.Combine(modsDir, modId.ToString());
        SteamWorkshopService.CreateDirectoryLink(linkPath, workshopItemPath);
        return Task.CompletedTask;
    }

    public static Task OnModRemovedAsync(string serverInstallPath, ulong modId, string modsSubDir)
    {
        SteamWorkshopService.RemoveDirectoryLink(Path.Combine(serverInstallPath, modsSubDir, modId.ToString()));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Group D — INI/config file update style.
/// Adds/removes mod entries in a specified config file section.
/// </summary>
internal static class GroupDHelper
{
    public static Task OnModDownloadedAsync(string serverInstallPath, string workshopItemPath,
        ulong modId, string modName, string configFile, string section)
    {
        var cfgPath = Path.Combine(serverInstallPath, configFile);
        if (!File.Exists(cfgPath)) return Task.CompletedTask;
        try
        {
            var lines  = File.ReadAllLines(cfgPath).ToList();
            var entry  = $"Mod={modId}";
            if (lines.Any(l => l.Trim().Equals(entry, StringComparison.OrdinalIgnoreCase)))
                return Task.CompletedTask;

            var secIdx = lines.FindIndex(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));
            if (secIdx >= 0)
                lines.Insert(secIdx + 1, entry);
            else
            {
                lines.Add($"[{section}]");
                lines.Add(entry);
            }
            File.WriteAllLines(cfgPath, lines);
        }
        catch { }
        return Task.CompletedTask;
    }

    public static Task OnModRemovedAsync(string serverInstallPath, ulong modId, string configFile)
    {
        var cfgPath = Path.Combine(serverInstallPath, configFile);
        if (!File.Exists(cfgPath)) return Task.CompletedTask;
        try
        {
            var entry = $"Mod={modId}";
            var lines = File.ReadAllLines(cfgPath)
                .Where(l => !l.Trim().Equals(entry, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            File.WriteAllLines(cfgPath, lines);
        }
        catch { }
        return Task.CompletedTask;
    }
}
