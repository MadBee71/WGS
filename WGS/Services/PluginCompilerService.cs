using System.IO;
using System.Reflection;
using WGS.Games;

namespace WGS.Services;

/// <summary>
/// Loads a pre-compiled plugin DLL and returns the first IGamePlugin found.
/// To create a plugin DLL: export the plugin as .cs, compile with
///   dotnet build -c Release
/// then import the resulting .dll here.
/// </summary>
public static class PluginCompilerService
{
    public static (IGamePlugin? plugin, string error) CompileAndLoad(string dllFilePath)
    {
        if (!File.Exists(dllFilePath))
            return (null, $"File not found: {dllFilePath}");

        Assembly assembly;
        try { assembly = Assembly.LoadFrom(dllFilePath); }
        catch (Exception ex) { return (null, $"Cannot load assembly: {ex.Message}"); }

        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IGamePlugin).IsAssignableFrom(t)
                              && !t.IsAbstract && !t.IsInterface);

        if (pluginType == null)
            return (null, "No class implementing IGamePlugin found in the DLL.");

        try
        {
            var plugin = (IGamePlugin?)Activator.CreateInstance(pluginType);
            return plugin is null
                ? (null, "Failed to instantiate plugin class.")
                : (plugin, string.Empty);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to create plugin instance: {ex.Message}");
        }
    }
}
