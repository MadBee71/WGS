using System.Collections.Generic;
using System.Threading.Tasks;
using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional plugin-owned server administration lists.
/// WGS supplies the generic UI; the game plugin owns native file/schema handling.
/// </summary>
public interface IGameAdminPlugin
{
    string AdminProviderName { get; }

    // Optional presentation metadata. Existing plugins automatically fall back
    // to their provider name / generic description.
    string AdminTabHeader => AdminProviderName;
    string AdminDescription => "Manage persistent game administration and access lists.";

    Task<IReadOnlyList<PluginAdminList>> LoadAdminListsAsync(GameServer server);
    Task<string> SaveAdminListAsync(GameServer server, string key, IReadOnlyList<string> values);
    string? GetServerAlertCommand(string message);
}

public sealed class PluginAdminList
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IdFirst { get; set; }
    public string Icon { get; set; } = "";
    public string InputHint { get; set; } = "";
    public List<string> Values { get; set; } = [];
}
