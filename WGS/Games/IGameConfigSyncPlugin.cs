using System.Threading.Tasks;
using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional extension contract for game plugins that own native game configuration
/// synchronization. WGS calls LoadSettingsFromConfigs when opening the server page.
/// The plugin remains responsible for saving before launch (normally from PreStartAsync).
/// </summary>
public interface IGameConfigSyncPlugin
{
    void LoadSettingsFromConfigs(GameServer server);
    Task SaveSettingsToConfigsAsync(GameServer server);
}
