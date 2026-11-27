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

    /// <summary>
    /// True when the server must not get redirected standard handles at all. WGS then starts it with a hidden console
    /// of its own, exactly as a normal command window would, and console commands cannot be sent to it.
    /// </summary>
    bool OwnHiddenConsole => false;

    /// <summary>Whether a file matching the pattern is the log of the run started at <paramref name="startedUtc"/>.</summary>
    bool IsRunLog(System.IO.FileInfo file, DateTime startedUtc) => file.CreationTimeUtc >= startedUtc.AddSeconds(-2);

    /// <summary>Process name used by earlier WGS versions, so a server they left running can still be reattached.</summary>
    string? LegacyProcessName => null;

    /// <summary>The text to show for a log line, or null to hide it.</summary>
    string? FormatLogLine(string line);
}
