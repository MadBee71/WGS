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
        Register(new DayZPlugin());
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
        Register(new WindRosePlugin());
        Register(new EnshroudedPlugin());
        Register(new IcarusPlugin());
        Register(new SoulmaskPlugin());
        // FPS
        Register(new CS2Plugin());
        Register(new CS16Plugin());
        Register(new CSCZPlugin());
        Register(new TF2Plugin());
        Register(new TFCPlugin());
        Register(new BlackMesaPlugin());
        Register(new GarrysModPlugin());
        Register(new KillingFloor2Plugin());
        Register(new InsurgencySandstormPlugin());
        Register(new MordhauPlugin());
        Register(new L4D2Plugin());
        Register(new DoDPlugin());
        Register(new DoDSPlugin());
        Register(new HLDMPlugin());
        Register(new HL2DMPlugin());
        Register(new HLOpForPlugin());
        // Open World
        Register(new FiveMPlugin());
        // Racing
        Register(new WreckfestPlugin());
        Register(new Wreckfest2Plugin());
        Register(new AssettoCorsaPlugin());
        Register(new AssettoCorsaCompetizionPlugin());
        // Military
        Register(new ArmaReforgerPlugin());
        Register(new ARMA3Plugin());
        Register(new ARMA2Plugin());
        Register(new SquadPlugin());
        // Simulation
        Register(new ETS2Plugin());
        Register(new AmericanTruckSimulatorPlugin());
        Register(new SatisfactoryPlugin());
        Register(new SpaceEngineersPlugin());
        // Survival (new)
        Register(new EmpyrionPlugin());
        Register(new BarotraumaPlugin());
        Register(new ProjectZomboidPlugin());
        Register(new UnturnedPlugin());
        Register(new CoreKeeperPlugin());
        // Other
        Register(new MinecraftPlugin());
        Register(new ForgePlugin());
        Register(new SpigotPlugin());
        Register(new FabricPlugin());
        Register(new TerrariaPlugin());
        Register(new RedMPlugin());
    }

    public static void Register(IGamePlugin plugin) => _plugins[plugin.GameId] = plugin;

    public static void Unregister(string gameId) => _plugins.Remove(gameId);

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

    /// <summary>Reads the saved custom game definitions without registering them — used to list what exists.</summary>
    public static List<CustomGameDefinition> ListCustomPlugins(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<CustomGameDefinition>>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    /// <summary>Removes a custom game both from the live registry and from disk.</summary>
    public static void RemoveCustomPlugin(string gameId, string path)
    {
        Unregister(gameId);
        var defs = ListCustomPlugins(path);
        defs.RemoveAll(d => d.GameId == gameId);
        File.WriteAllText(path, JsonSerializer.Serialize(defs,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
