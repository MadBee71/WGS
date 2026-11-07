using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace WGS.Services;

/// <summary>
/// Live player names for Enshrouded, parsed from the server's own console output. The server answers the A2S
/// player query with a correct count but empty names, so every player showed as "?" in all Players tabs
/// (GGRenegade, Discord, 6.11.2026). The log carries the real names:
///   "[I 14:26:05,228] [server] Player 'Skidish' logged in with Permissions:"
///   "[I 14:25:13,634] [server] Remove Player 'Skidish'"
/// </summary>
public class EnshroudedPlayerTracker
{
    private static readonly Regex JoinRegex  = new(@"\[server\]\s+Player '(.+)' logged in with Permissions", RegexOptions.Compiled);
    private static readonly Regex LeaveRegex = new(@"\[server\]\s+Remove Player '(.+)'\s*$",                  RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, DateTime> _connectedAtByName = new();

    public void OnLogLine(string line)
    {
        var join = JoinRegex.Match(line);
        if (join.Success)
        {
            _connectedAtByName[join.Groups[1].Value] = DateTime.UtcNow;
            return;
        }

        var leave = LeaveRegex.Match(line);
        if (leave.Success)
            _connectedAtByName.TryRemove(leave.Groups[1].Value, out _);
    }

    public List<(string Name, int ConnectedSeconds)> GetKnownPlayers() =>
        _connectedAtByName.OrderBy(kvp => kvp.Value).Select(kvp =>
            (Name: kvp.Key, ConnectedSeconds: (int)(DateTime.UtcNow - kvp.Value).TotalSeconds)).ToList();
}
