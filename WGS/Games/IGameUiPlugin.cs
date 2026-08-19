using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional interface for game plugins that want to contribute game-specific UI.
/// WGS owns the outer shell; the plugin supplies one or more tab definitions.
/// </summary>
public interface IGameUiPlugin
{
    IReadOnlyList<GamePluginTabDefinition> GetPluginTabs(GameServer server);
}

/// <summary>
/// Describes one game-specific tab supplied by a plugin.
/// CreateContent is invoked once when the server detail ViewModel is created.
/// The returned object is rendered by WPF's ContentPresenter.
/// </summary>
public sealed class GamePluginTabDefinition
{
    /// <summary>Stable plugin-owned page key, e.g. Overview, ConnectionInfo, Admin, World.</summary>
    public string Key { get; init; } = "";

    public required string Header { get; init; }
    public string? ToolTip { get; init; }
    public int Order { get; init; }

    /// <summary>
    /// Full page content rendered directly as a top-level server tab.
    /// </summary>
    public required Func<GameServer, object> CreateContent { get; init; }
}
