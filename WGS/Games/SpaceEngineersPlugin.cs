using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;
using WGS.Models;

namespace WGS.Games;

// The dedicated server needs a config file naming a world before it can run in console mode: "-console" skips the
// game's own setup window, so without one a fresh install exits with "Premade world not found". WGS therefore writes
// the config itself, in the server folder. Behaviour below verified against the real server 01_211_019 (26.11.2026).
public class SpaceEngineersPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin, IConsoleLogPlugin
{
    public override string GameId          => "spaceengineers";
    public override string GameName        => "Space Engineers";
    public override string Description     => "Engineering and survival sandbox set in space and on planets";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 298740;
    public override int    GameStoreAppId  => 244850;
    public override int    WorkshopAppId   => 244850;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"DedicatedServer64\SpaceEngineersDedicated.exe";
    public override int    DefaultPort      => 27016;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 16;
    // Ctrl+C makes the server save the world and exit within seconds.
    public override bool   UseCtrlCStop    => true;

    // Relative to the server folder, so each server has its own config and worlds and WGS backups include them.
    private const string DataFolder     = @"AppData\Roaming\SpaceEngineersDedicated";
    private const string ConfigName     = "SpaceEngineers-Dedicated.cfg";
    private const string PasswordMarker = "wgs-password";
    private const string OwnFolderOnly  = "This server's folder";
    private const string DefaultScenario = "Star System";

    public override List<string> ConfigFiles => [Path.Combine(DataFolder, ConfigName)];

    private static string DataPath(GameServer s) => Path.Combine(s.InstallPath, DataFolder);

    private static string SharedConfig => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceEngineersDedicated", ConfigName);

    // A server that was already set up with the game's own configurator (config in the user's %AppData%) keeps
    // running from that setup, untouched.
    private bool UsesOwnData(GameServer s) =>
        File.Exists(Path.Combine(DataPath(s), ConfigName))
        || S(s, "dataLocation") == OwnFolderOnly
        || !File.Exists(SharedConfig);

    public string A2SHost => "127.0.0.1";
    // The server answers queries on its game port.
    public int GetA2SPort(GameServer server) =>
        UsesOwnData(server) ? server.ServerPort : server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;

    private static bool HasIp(GameServer s) => !string.IsNullOrWhiteSpace(s.ServerIp) && s.ServerIp.Trim() != "0.0.0.0";

    public override string BuildStartArguments(GameServer s)
    {
        if (!UsesOwnData(s)) return "-console";
        // -port and -maxPlayers override the files: a loaded world keeps its own player limit, and the port may
        // have been changed by WGS's port check after PreStartAsync wrote the config.
        var args = $"-console -path \"{DataPath(s)}\" -port {s.ServerPort}";
        if (s.MaxPlayers > 0) args += $" -maxPlayers {s.MaxPlayers}";
        if (HasIp(s)) args += $" -ip {s.ServerIp.Trim()}";
        return args;
    }

    public override Task PreStartAsync(GameServer server)
    {
        if (!UsesOwnData(server)) return Task.CompletedTask;

        var data = DataPath(server);
        var path = Path.Combine(data, ConfigName);
        var worlds = Path.Combine(server.InstallPath, "Content", "CustomWorlds");
        var scenario = Path.Combine(worlds, S(server, "scenario", DefaultScenario));

        XDocument doc;
        if (File.Exists(path))
        {
            try { doc = XDocument.Load(path); }
            catch (System.Xml.XmlException ex) { throw new InvalidOperationException($"{path} is damaged: {ex.Message}"); }
        }
        else
        {
            if (!File.Exists(Path.Combine(scenario, "Sandbox_config.sbc"))) return Task.CompletedTask; // not installed yet
            doc = NewConfig(server, scenario);
        }

        var root = doc.Root ?? throw new InvalidOperationException($"{path} is empty.");
        root.SetElementValue("ServerPort", server.ServerPort);
        if (!string.IsNullOrWhiteSpace(server.ServerName)) root.SetElementValue("ServerName", server.ServerName);
        if (server.SteamPort > 0) root.SetElementValue("SteamPort", server.SteamPort);
        if (HasIp(server)) root.SetElementValue("IP", server.ServerIp.Trim());
        if (server.MaxPlayers > 0) root.Element("SessionSettings")?.SetElementValue("MaxPlayers", server.MaxPlayers);

        Directory.CreateDirectory(data);
        ApplyPassword(root, data, server.ServerPassword);
        RepairWorldPaths(root, data, worlds, scenario);
        doc.Save(path);
        return Task.CompletedTask;
    }

    // The server is a window program: without a console to inherit it opens its own (which WGS hides), so nothing
    // reaches stdout. Its log file in the data folder has the same messages.
    public (string Folder, string Pattern) GetConsoleLog(GameServer server) =>
        (UsesOwnData(server) ? DataPath(server) : Path.GetDirectoryName(SharedConfig)!, "SpaceEngineersDedicated_*.log");

    private static readonly System.Text.RegularExpressions.Regex LogLine =
        new(@"^\d{4}-\d\d-\d\d [\d:.]+ - Thread:\s*\d+ -> (?<text>.*)$");

    // Only the top levels of the log's indentation: startup, world, saves, players and errors. The deeper lines are
    // hundreds of definition-loading details.
    public string? FormatLogLine(string line)
    {
        var m = LogLine.Match(line);
        if (!m.Success) return null;
        var text = m.Groups["text"].Value;
        var message = text.Trim();
        return message.Length > 0 && text.Length - text.TrimStart().Length <= 5 ? message : null;
    }

    // The scenario's own settings become the session settings, as the game's configurator does.
    private XDocument NewConfig(GameServer server, string scenario)
    {
        var settings = XDocument.Load(Path.Combine(scenario, "Sandbox_config.sbc")).Root?.Element("Settings")?.Elements()
                       ?? throw new InvalidOperationException($"The scenario in {scenario} has no settings.");
        var session = new XElement("SessionSettings", settings);
        session.SetElementValue("OnlineMode", "PUBLIC");
        session.SetElementValue("GameMode", S(server, "gameMode", "Survival"));

        var worldName = string.Concat(S(server, "worldName").Split(Path.GetInvalidFileNameChars())).Trim().TrimEnd('.');
        if (worldName.Length == 0) worldName = "WGS World";

        return new XDocument(new XElement("MyConfigDedicated",
            new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            session,
            new XElement("LoadWorld"),
            new XElement("IP", "0.0.0.0"),
            new XElement("SteamPort", 8766),
            new XElement("ServerPort", server.ServerPort),
            new XElement("ServerName", server.ServerName),
            new XElement("WorldName", worldName),
            new XElement("PremadeCheckpointPath", scenario),
            // WGS does restarts and updates itself; the remote API is an extra network service nobody asked for.
            new XElement("AutoRestartEnabled", false),
            new XElement("AutoUpdateEnabled", false),
            new XElement("RemoteApiEnabled", false)));
    }

    // The game stores a PBKDF2-SHA1 hash (10000 iterations). An empty WGS password removes only a password WGS
    // itself set; one set by hand in the file stays.
    private static void ApplyPassword(XElement root, string data, string password)
    {
        var marker = Path.Combine(data, PasswordMarker);
        if (string.IsNullOrEmpty(password))
        {
            if (!File.Exists(marker)) return;
            root.Element("ServerPasswordHash")?.Remove();
            root.Element("ServerPasswordSalt")?.Remove();
            File.Delete(marker);
            return;
        }
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA1, 20);
        root.SetElementValue("ServerPasswordSalt", Convert.ToBase64String(salt));
        root.SetElementValue("ServerPasswordHash", Convert.ToBase64String(hash));
        File.WriteAllText(marker, "The password in the config file was set by WGS.");
    }

    // A config or world copied from another folder still names the old location. The server then reports
    // "World ... not found" and silently starts a NEW world, so point it at the copy that is here.
    private static void RepairWorldPaths(XElement root, string data, string worlds, string scenario)
    {
        var premade = (string?)root.Element("PremadeCheckpointPath") ?? "";
        if (premade.Length > 0 && !Directory.Exists(premade))
        {
            var same = Path.Combine(worlds, Path.GetFileName(premade.TrimEnd('\\', '/')));
            root.SetElementValue("PremadeCheckpointPath", Directory.Exists(same) ? same : scenario);
        }

        var saves = Path.Combine(data, "Saves");
        var load = (string?)root.Element("LoadWorld") ?? "";
        if (load.Length > 0 && (Directory.Exists(load) || File.Exists(load))) return;
        if (load.Length == 0 && File.Exists(Path.Combine(saves, "LastSession.sbl"))) return; // the server resumes it
        if (!Directory.Exists(saves)) return;

        static string Leaf(string p)
        {
            p = p.TrimEnd('\\', '/');
            if (p.EndsWith("Sandbox.sbc", StringComparison.OrdinalIgnoreCase)) p = Path.GetDirectoryName(p) ?? "";
            return Path.GetFileName(p);
        }
        static bool IsWorld(string dir) => File.Exists(Path.Combine(dir, "Sandbox.sbc"));

        var names = new[] { Leaf(load), (string?)root.Element("WorldName") ?? "" };
        var world = names.Where(n => n.Length > 0).Select(n => Path.Combine(saves, n)).FirstOrDefault(IsWorld);
        if (world == null)
        {
            var all = Directory.GetDirectories(saves).Where(IsWorld).ToList();
            if (all.Count == 1) world = all[0];
        }
        if (world != null) root.SetElementValue("LoadWorld", world);
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["scenario"]     = DefaultScenario,
        ["gameMode"]     = "Survival",
        ["worldName"]    = "WGS World",
        ["dataLocation"] = "Automatic",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.First(f => f.Key == "serverPass").Description =
            "Leave empty for no password. A password set by hand in the config file is kept.";
        fields.AddRange([
            new() { Key = "scenario", Label = "New world: scenario", FieldType = ConfigFieldType.Dropdown, DefaultValue = DefaultScenario,
                    Options = ["Star System", "Home System", "Distant Moons", "Green Station", "Asteroid Armory", "Dead Drop Arena",
                               "Lone Survivor", "Red Ship", "Red Ship 2", "Crashed Red Ship", "Pertam Orbiter", "Rival Platforms", "Empty World"],
                    Description = "Used only when the world is first created. Changing it later does not replace an existing world." },
            new() { Key = "gameMode", Label = "New world: game mode", FieldType = ConfigFieldType.Dropdown, DefaultValue = "Survival",
                    Options = ["Survival", "Creative"], Description = "Used only when the config file is first created." },
            new() { Key = "worldName", Label = "New world: name", FieldType = ConfigFieldType.Text, DefaultValue = "WGS World",
                    Description = "Used only when the config file is first created." },
            new() { Key = "dataLocation", Label = "Config and worlds", FieldType = ConfigFieldType.Dropdown, DefaultValue = "Automatic",
                    Options = ["Automatic", OwnFolderOnly],
                    Description = "Automatic: kept in this server's folder (AppData\\Roaming\\SpaceEngineersDedicated), unless a setup made with the game's own configurator already exists in %AppData% — that one is then used unchanged." },
        ]);
        return fields;
    }
}
