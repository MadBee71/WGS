namespace WGS.Games;

/// <summary>
/// Implemented by game plugins that support Steam Workshop mods.
/// SteamWorkshopService calls these hooks after download/removal so that
/// each game can set up symlinks, copy keys, and patch config files.
/// </summary>
public interface IWorkshopPlugin
{
    /// <summary>
    /// Subdirectory (relative to server install root) where the game expects mods to live.
    /// Empty string means mods live directly under the install root (Group A symlinks).
    /// </summary>
    string ModTargetDirectory { get; }

    /// <summary>
    /// Called once SteamCMD has finished downloading <paramref name="modId"/>.
    /// Implementations should create symlinks / junctions, copy keys, patch config, etc.
    /// </summary>
    /// <param name="serverInstallPath">Absolute path to the game server's install folder.</param>
    /// <param name="workshopItemPath">Absolute path to the downloaded Workshop item folder
    ///     ({steamcmd}/steamapps/workshop/content/{workshopAppId}/{modId}).</param>
    /// <param name="modId">Steam Workshop item ID.</param>
    /// <param name="modName">Display name resolved from the Steam API (or modId as fallback).</param>
    Task OnModDownloadedAsync(string serverInstallPath, string workshopItemPath, ulong modId, string modName);

    /// <summary>
    /// Called before the Workshop item folder is deleted.
    /// Implementations should remove symlinks, keys, config entries, etc.
    /// </summary>
    Task OnModRemovedAsync(string serverInstallPath, string workshopItemPath, ulong modId, string modName);

    /// <summary>
    /// Returns the server startup argument(s) that activate the listed mods, or empty string if none needed.
    /// </summary>
    string BuildModArguments(IReadOnlyList<ulong> activeModIds, string serverInstallPath);
}
