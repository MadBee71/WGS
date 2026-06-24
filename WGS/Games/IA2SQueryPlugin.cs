namespace WGS.Games;

/// <summary>
/// Implemented by plugins that use A2S UDP query for player counts
/// instead of RCON (e.g. ARMA Reforger).
/// </summary>
public interface IA2SQueryPlugin
{
    /// <summary>Host to query (usually "127.0.0.1").</summary>
    string A2SHost { get; }

    /// <summary>UDP port for A2S queries (the game's -a2sPort / query port).</summary>
    int GetA2SPort(Models.GameServer server);
}
