using System.Collections.Generic;
using System.Threading.Tasks;
using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional plugin-owned mod manager. The WGS host supplies a generic Mods tab while
/// the game plugin owns discovery, install/update/remove, dependency and package logic.
/// </summary>
public interface IGameModsPlugin
{
    string ModsProviderName { get; }

    // Optional presentation metadata.
    string ModsTabHeader => "Mods";
    string ModsDescription => $"Search, install, update, and remove mods using {ModsProviderName}.";

    /// <summary>Rarely changed provider settings shown in a collapsed section at the bottom of the Mods tab.</summary>
    IReadOnlyList<ConfigField> GetModsSettings() => [];

    Task<IReadOnlyList<PluginModSearchResult>> SearchModsAsync(GameServer server, string query);
    Task<IReadOnlyList<PluginInstalledMod>> GetInstalledModsAsync(GameServer server);
    Task<string> InstallModAsync(GameServer server, PluginModSearchResult mod);
    Task<string> RemoveModAsync(GameServer server, PluginInstalledMod mod);
}

public sealed class PluginModSearchResult
{
    public long GameId { get; set; }
    public long ModId { get; set; }
    public long FileId { get; set; }
    public string Name { get; set; } = "";
    public string NameId { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Filename { get; set; } = "";
    public string ProfileUrl { get; set; } = "";
    public bool IsInstalled { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public string RoleBadge { get; set; } = "";
    public string ActionText => IsUpdateAvailable ? "Update" : IsInstalled ? "Reinstall" : "Install";
}

public sealed class PluginInstalledMod
{
    public string PackageKey { get; set; } = "";
    public long GameId { get; set; }
    public long ModId { get; set; }
    public long FileId { get; set; }
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string RoleBadge { get; set; } = "";
    public string DependencyOf { get; set; } = "";
    public List<string> Files { get; set; } = [];
    public string DisplayRole => string.IsNullOrWhiteSpace(RoleBadge) ? "MOD" : RoleBadge;
}
