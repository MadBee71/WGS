namespace WGS.Games;

/// <summary>
/// Optional metadata for runtime/imported game plugins.
/// Plugins that do not implement this interface continue to work as before.
/// </summary>
public interface IGamePluginMetadata
{
    string PluginVersion { get; }
    string MinimumWgsVersion { get; }
}
