using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional plugin parser for real-time join/leave lines emitted by the game or a
/// server-side helper module such as NidToolbox.
/// </summary>
public interface IGamePlayerEventPlugin
{
    PluginPlayerEvent? ParsePlayerEvent(string line);
}

public sealed class PluginPlayerEvent
{
    public bool Joined { get; set; }
    public string Name { get; set; } = "";
    public string SteamId { get; set; } = "";
    public string IpAddress { get; set; } = "";
}
