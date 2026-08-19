using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional generic dashboard/actions supplied by a game plugin.
/// WGS renders the cards/buttons; the plugin owns the values and command behavior.
/// </summary>
public interface IGameDashboardPlugin
{
    // Optional presentation metadata for the generic host surface.
    string DashboardTabHeader => "Overview";
    string DashboardDescription => "Game-specific overview supplied by the active plugin.";

    IReadOnlyList<GameDashboardCard> GetDashboardCards(GameServer server);
    IReadOnlyList<GamePluginAction> GetPluginActions(GameServer server);
}

public sealed class GameDashboardCard
{
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public int Order { get; init; }
}

public sealed class GamePluginAction
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public bool RequiresStoppedServer { get; init; }
    public bool RequiresRunningServer { get; init; }

    /// <summary>
    /// Executes the plugin-defined action. Return value is displayed as status text.
    /// </summary>
    public required Func<GameServer, Task<string>> ExecuteAsync { get; init; }
}
