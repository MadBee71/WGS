using System.IO;
using System.Text.Json;
using WGS.Games;

namespace WGS.Services;

public sealed class PluginManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string MinimumWgsVersion { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool OverrideBuiltIn { get; set; } = true;
}

public static class PluginFolderHost
{
    private static readonly HashSet<IGamePlugin> _runtimePlugins =
        new(ReferenceEqualityComparer.Instance);

    public static bool IsRuntimePlugin(IGamePlugin? plugin)
        => plugin != null && _runtimePlugins.Contains(plugin);

    private static void MarkRuntimePlugin(IGamePlugin plugin)
        => _runtimePlugins.Add(plugin);
    public static string PluginsRoot
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "WGS", "Plugins");
        }
    }
    public static string LogPath => Path.Combine(PluginsRoot, "plugin-host.log");

    /// <summary>
    /// Installs or updates a single-file source plugin into the persistent plugin folder.
    /// The plugin is compiled first; disk state is changed only after successful compilation.
    /// Existing plugin source is backed up before replacement.
    /// </summary>
    public static (IGamePlugin? plugin, string error, bool updated) InstallOrUpdate(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            return (null, "Plugin source file was not found.", false);

        var (plugin, compileError) = PluginCompilerService.CompileAndLoad(sourcePath);
        if (plugin == null)
            return (null, compileError, false);

        var metadata = plugin as IGamePluginMetadata;
        var pluginVersion = metadata?.PluginVersion ?? "";
        var minimumWgsVersion = metadata?.MinimumWgsVersion ?? "";

        if (!IsHostCompatible(minimumWgsVersion))
        {
            return (null,
                $"Plugin '{plugin.GameName}' requires WGS {minimumWgsVersion} or newer. " +
                $"Current WGS version is {AppInfo.Version}.",
                false);
        }

        var safeId = new string(plugin.GameId
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
            .ToArray());

        if (string.IsNullOrWhiteSpace(safeId))
            return (null, "Plugin returned an invalid GameId.", false);

        Directory.CreateDirectory(PluginsRoot);
        var pluginDir = Path.Combine(PluginsRoot, safeId);
        var existed = Directory.Exists(pluginDir);
        Directory.CreateDirectory(pluginDir);

        var installedSource = Path.Combine(pluginDir, "Plugin.cs");
        if (File.Exists(installedSource))
        {
            var backupDir = Path.Combine(pluginDir, "backups");
            Directory.CreateDirectory(backupDir);
            var backup = Path.Combine(backupDir,
                $"Plugin-{DateTime.Now:yyyyMMdd-HHmmss}.cs.bak");
            File.Copy(installedSource, backup, overwrite: false);
        }

        File.Copy(sourcePath, installedSource, overwrite: true);

        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        PluginManifest manifest;
        try
        {
            manifest = File.Exists(manifestPath)
                ? JsonSerializer.Deserialize<PluginManifest>(
                    File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new PluginManifest()
                : new PluginManifest();
        }
        catch
        {
            manifest = new PluginManifest();
        }

        manifest.Id = plugin.GameId;
        manifest.Name = plugin.GameName;
        manifest.Version = pluginVersion;
        manifest.MinimumWgsVersion = minimumWgsVersion;
        manifest.Enabled = true;
        manifest.OverrideBuiltIn = true;

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest,
            new JsonSerializerOptions { WriteIndented = true }));

        // Register immediately; Register intentionally replaces an existing GameId.
        MarkRuntimePlugin(plugin);
        GameRegistry.Register(plugin);
        Log($"{(existed ? "UPDATE" : "INSTALL")} {plugin.GameName}: registered '{plugin.GameId}'");

        return (plugin, string.Empty, existed);
    }

    public static void LoadAll()
    {
        Directory.CreateDirectory(PluginsRoot);
        Log("=== plugin scan ===");

        foreach (var dir in Directory.GetDirectories(PluginsRoot).OrderBy(x => x))
        {
            var manifestPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                var manifest = JsonSerializer.Deserialize<PluginManifest>(
                    File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (manifest == null || !manifest.Enabled)
                {
                    Log($"SKIP {Path.GetFileName(dir)}: disabled or invalid manifest");
                    continue;
                }

                var sources = Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly)
                    .OrderBy(x => x)
                    .ToArray();

                var (plugin, error) = PluginCompilerService.CompileAndLoadFiles(
                    sources,
                    string.IsNullOrWhiteSpace(manifest.Id) ? Path.GetFileName(dir) : manifest.Id);

                if (plugin == null)
                {
                    Log($"ERROR {manifest.Name}: {error.Replace(Environment.NewLine, " | ")}");
                    continue;
                }

                var metadata = plugin as IGamePluginMetadata;
                var requiredWgs = !string.IsNullOrWhiteSpace(manifest.MinimumWgsVersion)
                    ? manifest.MinimumWgsVersion
                    : metadata?.MinimumWgsVersion ?? "";

                if (!IsHostCompatible(requiredWgs))
                {
                    Log($"SKIP {manifest.Name}: requires WGS {requiredWgs} or newer; current is {AppInfo.Version}");
                    continue;
                }

                if (!manifest.OverrideBuiltIn && GameRegistry.Get(plugin.GameId) != null)
                {
                    Log($"SKIP {manifest.Name}: game id '{plugin.GameId}' already registered");
                    continue;
                }

                MarkRuntimePlugin(plugin);
                GameRegistry.Register(plugin);
                var loadedVersion = !string.IsNullOrWhiteSpace(manifest.Version)
                    ? manifest.Version
                    : metadata?.PluginVersion ?? "";
                Log($"OK {manifest.Name} {loadedVersion}: registered '{plugin.GameId}'");
            }
            catch (Exception ex)
            {
                Log($"ERROR {Path.GetFileName(dir)}: {ex.Message}");
            }
        }
    }

    public static bool IsHostCompatible(string? minimumWgsVersion)
    {
        if (string.IsNullOrWhiteSpace(minimumWgsVersion))
            return true;

        if (!Version.TryParse(minimumWgsVersion, out var minimum))
            return true;

        if (!Version.TryParse(AppInfo.Version, out var current))
            return true;

        return current >= minimum;
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
