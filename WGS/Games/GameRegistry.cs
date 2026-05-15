using System.IO;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public static class GameRegistry
{
    private static readonly Dictionary<string, IGamePlugin> _plugins = new();

    static GameRegistry()
    {
        // Survival
        Register(new ValheimPlugin());
        Register(new RustPlugin());
        Register(new SevenDaysToDiePlugin());
        Register(new ConanExilesPlugin());
        Register(new ARKSEPlugin());
        Register(new SonsOfTheForestPlugin());
        Register(new TheForestPlugin());
        Register(new STNPlugin());
        Register(new SCUMPlugin());
        Register(new VeinPlugin());
        Register(new PalworldPlugin());
        Register(new VRisingPlugin());
        Register(new DSTPlugin());
        Register(new TheIslePlugin());
        Register(new ReturnToMoriaPlugin());
        Register(new ASTRONEERPlugin());
        Register(new LongvinterPlugin());
        Register(new NoOneSurvivedPlugin());
        Register(new ASKAPlugin());
        Register(new NecessePlugin());
        Register(new RisingWorldPlugin());
        Register(new SunkenlandPlugin());
        // FPS
        Register(new CS2Plugin());
        Register(new BlackMesaPlugin());
        Register(new GarrysModPlugin());
        // Racing
        Register(new WreckfestPlugin());
        Register(new Wreckfest2Plugin());
        Register(new AssettoCorsaPlugin());
        // Military
        Register(new ArmaReforgerPlugin());
        Register(new ARMA3Plugin());
        Register(new ARMA2Plugin());
        // Simulation
        Register(new ETS2Plugin());
        Register(new AmericanTruckSimulatorPlugin());
        Register(new SatisfactoryPlugin());
        // Other
        Register(new MinecraftPlugin());
        Register(new TerrariaPlugin());
        Register(new RedMPlugin());
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
