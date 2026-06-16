namespace WGS.Games;

/// <summary>
/// Implement on plugins that fetch player lists via HTTP REST instead of RCON.
/// ServerViewModel calls GetPlayersAsync() when RCON is unavailable and this interface is present.
/// </summary>
public interface IRestPlayersPlugin
{
    /// <summary>Returns the base URL for the REST API, e.g. "http://127.0.0.1:8212".</summary>
    string GetRestApiBaseUrl(Models.GameServer server);

    /// <summary>Fetches the player list and returns it. Returns empty list on failure.</summary>
    Task<List<Models.OnlinePlayer>> GetPlayersAsync(Models.GameServer server);

    /// <summary>Diagnostic message describing the most recent fetch failure, or null if the last fetch succeeded.</summary>
    string? LastRestApiError { get; }
}
