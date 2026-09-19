using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace WGS.Services;

/// <summary>
/// Live player list for Factorio, parsed from the vanilla dedicated server's own console output —
/// no RCON or mod required. Exists because FactorioPlugin implements neither IA2SQueryPlugin (its
/// UDP discovery protocol isn't Source A2S) nor IRestPlayersPlugin, so without this the Players
/// tab always reported 0 online regardless of who was actually connected.
///
/// Factorio prints join/leave events with a real wall-clock timestamp prefix even though the rest
/// of its log uses tick-relative seconds, e.g.:
///   "2026-09-18 16:13:34 [JOIN] Test joined the game"
///   "2026-09-18 16:19:12 [LEAVE] Test left the game"
/// </summary>
public class FactorioPlayerTracker
{
    private static readonly Regex JoinRegex  = new(@"\[JOIN\]\s+(.+?)\s+joined the game", RegexOptions.Compiled);
    private static readonly Regex LeaveRegex = new(@"\[LEAVE\]\s+(.+?)\s+left the game",   RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, DateTime> _connectedAtByName = new();

    public void OnLogLine(string line)
    {
        var join = JoinRegex.Match(line);
        if (join.Success)
        {
            _connectedAtByName[join.Groups[1].Value.Trim()] = DateTime.UtcNow;
            return;
        }

        var leave = LeaveRegex.Match(line);
        if (leave.Success)
            _connectedAtByName.TryRemove(leave.Groups[1].Value.Trim(), out _);
    }

    public List<(string Name, int ConnectedSeconds)> GetKnownPlayers() =>
        _connectedAtByName.Select(kvp =>
            (Name: kvp.Key, ConnectedSeconds: (int)(DateTime.UtcNow - kvp.Value).TotalSeconds)).ToList();
}
