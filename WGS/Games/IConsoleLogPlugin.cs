using WGS.Models;

namespace WGS.Games;

/// <summary>
/// For servers that write their console output to their own console window and log file instead of the stdout WGS
/// captures (a window program that allocates a console, e.g. Space Engineers). WGS then shows the newest log file
/// created by the run in its console.
/// </summary>
public interface IConsoleLogPlugin
{
    (string Folder, string Pattern) GetConsoleLog(GameServer server);

    /// <summary>The text to show for a log line, or null to hide it.</summary>
    string? FormatLogLine(string line);
}
