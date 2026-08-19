using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional navigation/presentation control for rich game plugins.
/// WGS remains game-agnostic: plugins identify built-in host tabs by stable keys,
/// can rename/hide/reorder them, and may replace a built-in tab's content.
/// </summary>
public interface IGameNavigationPlugin
{
    IReadOnlyList<GameBuiltInTabOverride> GetBuiltInTabOverrides(GameServer server);
}

public sealed class GameBuiltInTabOverride
{
    /// <summary>
    /// Stable WGS host key such as:
    /// Backups, Info, Mods, Schedule, Players, Charts, Files,
    /// ActionLog, LogWatcher, Console, Settings, Config.
    ///
    /// Generic rich-provider host surfaces also use stable keys:
    /// PluginOverview, PluginAdmin, PluginMods.
    /// </summary>
    public required string Key { get; init; }

    public string? Header { get; init; }
    public int? Order { get; init; }
    public bool Visible { get; init; } = true;

    /// <summary>
    /// Optional full replacement page for this built-in tab.
    /// This lets a plugin keep the host tab position/name while owning all content.
    /// </summary>
    public Func<GameServer, object>? ReplaceContent { get; init; }
}
