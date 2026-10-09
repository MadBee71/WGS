using System.IO;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Manages SourceMod .smx plugin files for Source engine game servers.
/// Active plugins live in  addons/sourcemod/plugins/
/// Disabled plugins live in addons/sourcemod/plugins/disabled/
/// </summary>
public class SourceModService
{
    private static string ActiveDir(string installPath)
        => Path.Combine(installPath, "addons", "sourcemod", "plugins");

    private static string DisabledDir(string installPath)
        => Path.Combine(installPath, "addons", "sourcemod", "plugins", "disabled");

    /// <summary>Returns true if the SourceMod addons folder exists in the server install path.</summary>
    public static bool IsInstalled(string installPath)
        => Directory.Exists(ActiveDir(installPath));

    /// <summary>
    /// The folder that actually contains "addons/sourcemod". On a real srcds install that is the
    /// game's own folder (e.g. &lt;install&gt;\left4dead2\addons\sourcemod), NOT the install root —
    /// WGS used to look only at the install root, so it never found (or moved files in) the real
    /// SourceMod. Order: the game's folder, then the install root (the old behaviour, kept so nobody
    /// who relied on it loses the tab), then any direct sub-folder that has SourceMod.
    /// </summary>
    public static string ResolveAddonsRoot(IGamePlugin? plugin, string installPath)
    {
        static bool HasPlugins(string root) => Directory.Exists(ActiveDir(root));
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath)) return installPath;

        var gameDir = plugin?.SourceModGameDir;
        if (!string.IsNullOrEmpty(gameDir))
        {
            var real = Path.Combine(installPath, gameDir);
            if (HasPlugins(real)) return real;
        }
        if (HasPlugins(installPath)) return installPath;

        try
        {
            foreach (var sub in Directory.EnumerateDirectories(installPath))
                if (HasPlugins(sub)) return sub;
        }
        catch { /* unreadable folder — fall through to the install root */ }
        return installPath;
    }

    /// <summary>The plugins folder WGS manages for this server (for display).</summary>
    public static string PluginsFolder(IGamePlugin? plugin, string installPath)
        => ActiveDir(ResolveAddonsRoot(plugin, installPath));

    /// <summary>Returns true if the plugin supports SourceMod and has it installed.</summary>
    public static bool IsAvailable(IGamePlugin? plugin, string installPath)
        => plugin?.SupportsSourceMod == true && IsInstalled(ResolveAddonsRoot(plugin, installPath));

    /// <summary>Lists all active .smx plugin files (filename only, no extension).</summary>
    public List<SourceModPlugin> GetActivePlugins(string installPath)
    {
        var dir = ActiveDir(installPath);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.smx", SearchOption.TopDirectoryOnly)
            .Select(f => new SourceModPlugin(Path.GetFileName(f), false))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Lists all disabled .smx plugin files (filename only).</summary>
    public List<SourceModPlugin> GetDisabledPlugins(string installPath)
    {
        var dir = DisabledDir(installPath);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.smx", SearchOption.TopDirectoryOnly)
            .Select(f => new SourceModPlugin(Path.GetFileName(f), true))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Moves a plugin from active → disabled folder.</summary>
    public void DisablePlugin(string installPath, string fileName)
    {
        var src  = Path.Combine(ActiveDir(installPath), fileName);
        var dest = Path.Combine(DisabledDir(installPath), fileName);
        // Was a silent return — the button looked dead. Report it so the Plugins tab can show why.
        if (!File.Exists(src)) throw new FileNotFoundException($"Plugin file not found: {src}");
        Directory.CreateDirectory(DisabledDir(installPath));
        File.Move(src, dest, overwrite: true);
    }

    /// <summary>Moves a plugin from disabled → active folder.</summary>
    public void EnablePlugin(string installPath, string fileName)
    {
        var src  = Path.Combine(DisabledDir(installPath), fileName);
        var dest = Path.Combine(ActiveDir(installPath), fileName);
        if (!File.Exists(src)) throw new FileNotFoundException($"Plugin file not found: {src}");
        File.Move(src, dest, overwrite: true);
    }
}

public record SourceModPlugin(string FileName, bool IsDisabled)
{
    /// <summary>Display name without .smx extension.</summary>
    public string Name => Path.GetFileNameWithoutExtension(FileName);
}
