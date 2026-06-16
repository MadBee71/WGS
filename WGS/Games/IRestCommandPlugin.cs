namespace WGS.Games;

/// <summary>
/// Implement on plugins whose console commands must go through a REST API instead of
/// process stdin (e.g. because the process runs with a native console window and has no
/// redirected stdin). ServerManagerService tries this before falling back to stdin.
/// </summary>
public interface IRestCommandPlugin
{
    /// <summary>
    /// Attempts to translate and send a free-text console command via REST API.
    /// Returns (true, response) if the command was recognized and sent, (false, "") if
    /// the command text wasn't recognized and the caller should fall back to stdin.
    /// </summary>
    Task<(bool handled, string response)> TrySendRestCommandAsync(Models.GameServer server, string command);
}
