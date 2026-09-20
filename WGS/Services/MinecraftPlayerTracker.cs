using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace WGS.Services;

/// <summary>
/// Live player list for Minecraft Java Edition (vanilla, Paper/Spigot, Forge, NeoForge, Fabric —
/// same core log lines regardless of mod loader), parsed from the server's own console output.
/// Exists because the Server List Ping fallback (MinecraftSLPService, used when RCON isn't
/// connected) only reports a player COUNT, never names — LocalServerBackend.GetOnlinePlayersAsync
/// was filling the gap with "?" placeholders that never got a real name or a ticking connected
/// duration (DatBrokeBoi, Discord forum, 20.9.2026: "show ? as players... Playtime in playing now
/// doesn't update"). History/Statistics/Activity looked fine only because a single "?" identity
/// still produces a correct-looking session when just one player is online at a time — it would
/// have collided the moment two players were connected simultaneously.
/// </summary>
public class MinecraftPlayerTracker
{
    private static readonly Regex JoinRegex  = new(@":\s*(.+?)\s+joined the game$",  RegexOptions.Compiled);
    private static readonly Regex LeaveRegex = new(@":\s*(.+?)\s+left the game$",    RegexOptions.Compiled);

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
