using System.IO;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public static class GameRegistry
{
    private static readonly Dictionary<string, IGamePlugin> _plugins = new();

    static GameRegistry()
    {
        Register(new ValheimPlugin());
        Register(new MinecraftPlugin());
        Register(new ConanExilesPlugin());
        Register(new RustPlugin());
        Register(new SevenDaysToDiePlugin());
        Register(new ETS2Plugin());
        Register(new STNPlugin());
        Register(new WreckfestPlugin());
        Register(new Wreckfest2Plugin());
        Register(new TheForestPlugin());
        Register(new SonsOfTheForestPlugin());
        Register(new ArmaReforgerPlugin());
        Register(new ARKSEPlugin());
        Register(new BlackMesaPlugin());
        Register(new SCUMPlugin());
        Register(new VeinPlugin());
        Register(new AssettoCorsaPlugin());
    }

    public static void Register(IGamePlugin plugin) => _plugins[plugin.GameId] = plugin;

    public static IGamePlugin? Get(string gameId)
        => _plugins.TryGetValue(gameId, out var p) ? p : null;

    public static IEnumerable<IGamePlugin> All => _plugins.Values;

    public static IEnumerable<IGrouping<string, IGamePlugin>> ByCategory
        => _plugins.Values.GroupBy(p => p.Category);

    public static void LoadCustomPlugins(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var defs = JsonSerializer.Deserialize<List<CustomGameDefinition>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (defs == null) return;
            foreach (var def in defs)
                if (!string.IsNullOrWhiteSpace(def.GameId))
                    Register(new CustomGamePlugin(def));
        }
        catch { /* silently ignore corrupt files */ }
    }

    public static void SaveCustomPlugin(CustomGameDefinition def, string path)
    {
        var defs = new List<CustomGameDefinition>();
        if (File.Exists(path))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<List<CustomGameDefinition>>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (existing != null) defs.AddRange(existing);
            }
            catch { }
        }

        var idx = defs.FindIndex(d => d.GameId == def.GameId);
        if (idx >= 0) defs[idx] = def;
        else defs.Add(def);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(defs,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
