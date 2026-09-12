using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace WGS.Services;

/// <summary>
/// Best-effort SteamID→name/connect-time tracking for Valheim, parsed live from the vanilla
/// dedicated server's own console output — no mod required. Exists because Valheim's A2S_PLAYER
/// response never includes real player names or a usable duration (see
/// A2SQueryService.ParsePlayers), so the Players tab would otherwise show "?" and 0s forever.
///
/// Three log line shapes carry what's needed, correlated by SteamID:
///   "Got connection SteamID 76561..."        — a peer connected; timestamp recorded here doubles
///                                               as the connect time used for play duration
///   "Got character ZDOID from Jonsku : ..."   — the next pending connection's name resolves
///   "Closing socket 76561..."                 — that SteamID disconnected, drop its entry
///
/// The connect→name link is positional, not explicit (Valheim doesn't print the SteamID on the
/// character line) — pending SteamIDs are matched to character lines in arrival order, which
/// holds up under normal play but could mismatch if many peers connect in the same instant.
/// That's an accepted best-effort limitation, not a correctness guarantee. Ping is not available
/// from any source WGS can read for Valheim (A2S_PLAYER has no ping field, and nothing in the
/// console output carries live RTT) — not tracked here.
/// </summary>
public class ValheimPlayerTracker
{
    private static readonly Regex ConnectRegex    = new(@"Got connection SteamID (\d+)", RegexOptions.Compiled);
    private static readonly Regex CharacterRegex  = new(@"Got character ZDOID from (.+?) :", RegexOptions.Compiled);
    private static readonly Regex DisconnectRegex = new(@"Closing socket (\d+)", RegexOptions.Compiled);

    private readonly ConcurrentQueue<string> _pendingSteamIds = new();
    private readonly ConcurrentDictionary<string, DateTime> _connectedAtBySteamId = new();
    private readonly ConcurrentDictionary<string, string>   _namesBySteamId = new();

    public void OnLogLine(string line)
    {
        var connect = ConnectRegex.Match(line);
        if (connect.Success)
        {
            var steamId = connect.Groups[1].Value;
            _connectedAtBySteamId[steamId] = DateTime.UtcNow;
            _pendingSteamIds.Enqueue(steamId);
            return;
        }

        var character = CharacterRegex.Match(line);
        if (character.Success)
        {
            if (_pendingSteamIds.TryDequeue(out var steamId))
                _namesBySteamId[steamId] = character.Groups[1].Value.Trim();
            return;
        }

        var disconnect = DisconnectRegex.Match(line);
        if (disconnect.Success)
        {
            var steamId = disconnect.Groups[1].Value;
            _namesBySteamId.TryRemove(steamId, out _);
            _connectedAtBySteamId.TryRemove(steamId, out _);
        }
    }

    /// <summary>Currently known (name, SteamID64, seconds connected) entries — order doesn't matter
    /// here, callers just need up to N real entries to replace "?"/0s placeholders for however
    /// many are actually online.</summary>
    public List<(string Name, string SteamId, int ConnectedSeconds)> GetKnownPlayers() =>
        _namesBySteamId.Select(kvp =>
        {
            var seconds = _connectedAtBySteamId.TryGetValue(kvp.Key, out var connectedAt)
                ? (int)(DateTime.UtcNow - connectedAt).TotalSeconds
                : 0;
            return (Name: kvp.Value, SteamId: kvp.Key, ConnectedSeconds: seconds);
        }).ToList();
}
