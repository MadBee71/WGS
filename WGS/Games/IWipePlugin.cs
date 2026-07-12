using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Implemented by plugins that support scheduled world wipes.
/// Returns the glob patterns (relative to InstallPath) that should be deleted on a full wipe.
/// </summary>
public interface IWipePlugin
{
    /// <summary>
    /// Glob patterns of files/directories to delete for a full wipe (map + player data).
    /// Paths are relative to <see cref="GameServer.InstallPath"/>.
    /// </summary>
    IEnumerable<string> GetFullWipePaths(GameServer server);

    /// <summary>
    /// Glob patterns for a map-only wipe (world data, no blueprints/player data removed).
    /// Return the same as <see cref="GetFullWipePaths"/> if there is no distinction.
    /// </summary>
    IEnumerable<string> GetMapWipePaths(GameServer server);
}
