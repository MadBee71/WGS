using WGS.Models;

namespace WGS.Games;

/// <summary>
/// For servers with their own way of telling whether they are alive. The Health Check uses this instead of the
/// A2S/REST/RCON probes, and only counts failures after the running server has answered at least once, so a long
/// start-up or save load is not mistaken for a freeze.
/// </summary>
public interface IHealthProbePlugin
{
    Task<bool> IsAliveAsync(GameServer server);
}
