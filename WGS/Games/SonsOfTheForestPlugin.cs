using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WGS.Models;

namespace WGS.Games;

public class SonsOfTheForestPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId        => "sonsoftheforest";
    public override string GameName      => "Sons of the Forest";
    public override string Description   => "Survival horror sequel on a cannibal island";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 2465200;
    public override int    GameStoreAppId => 1326470;
    public override int    WorkshopAppId   => 2465200;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable    => "SonsOfTheForestDS.exe";
    public override int    DefaultPort   => 8766;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;
    public override bool   HasRcon       => true;
    protected override bool FilterUnityShaderNoise => true;


    public string A2SHost => "127.0.0.1";

    /// <summary>The user-data copy of dedicatedserver.cfg is the one the game actually reads; listed (absolute path) so the Config tab edits it.</summary>
    public static string UserDataConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Endnight", "SonsOfTheForestDS", "dedicatedserver.cfg");
    public override List<string> ConfigFiles => [UserDataConfigPath];
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => string.Empty; // config via dedicatedserver.cfg

    // Merges WGS-managed fields into existing config to preserve user customizations
    public override async Task PreStartAsync(GameServer server)
    {
        // The game keeps its dedicatedserver.cfg in the user data folder (AppData\LocalLow\Endnight\SonsOfTheForestDS) unless
        // -userdatapath says otherwise; some guides place it in the server root. Merge into both so an existing file there is
        // read and kept (GitHub #18) and the GUI settings reach whichever one the game loads.
        var userData = Path.GetDirectoryName(UserDataConfigPath)!;
        Directory.CreateDirectory(userData);   // also on the very first start, so the game finds our file instead of generating defaults
        var paths = new List<string>
        {
            Path.Combine(server.InstallPath, "dedicatedserver.cfg"),
            Path.Combine(userData, "dedicatedserver.cfg"),
        };
        foreach (var p in paths) await MergeConfigAsync(server, p);
    }

    private async Task MergeConfigAsync(GameServer server, string cfgPath)
    {
        var maxPlayers = Math.Min(server.MaxPlayers, 8); // game hard cap
        var gameMode   = S(server, "GameDifficulty", "Normal");
        var treeRegen  = S(server, "RegenerationEnabled", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        JsonObject obj = new();
        if (File.Exists(cfgPath))
        {
            try { obj = JsonNode.Parse(await File.ReadAllTextAsync(cfgPath))?.AsObject() ?? new(); }
            catch { obj = new(); }
        }

        // The Settings tab's IP address wins when the user set one; otherwise a bind address already in the file (e.g. a specific
        // NIC on a multi-IP host) is kept, and 0.0.0.0 is used only when there is none.
        var ip = server.ServerIp?.Trim();
        if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0") obj["IpAddress"] = ip;
        else if (string.IsNullOrWhiteSpace(obj["IpAddress"]?.ToString())) obj["IpAddress"] = "0.0.0.0";
        obj["GamePort"]   = server.ServerPort;
        obj["QueryPort"]  = server.QueryPort;
        obj["ServerName"] = server.ServerName;
        obj["MaxPlayers"] = maxPlayers;
        obj["GameMode"]   = gameMode;
        // The GUI's Password field (server.ServerPassword) — it is not in GameSpecificSettings, so reading it from there
        // always gave "". Applied every start; an empty GUI password leaves a password set by hand in the file alone.
        if (!string.IsNullOrEmpty(server.ServerPassword)) obj["Password"] = server.ServerPassword;
        else if (!obj.ContainsKey("Password"))            obj["Password"] = "";
        if (!obj.ContainsKey("BlobSyncPort")) obj["BlobSyncPort"] = 9700;
        if (!obj.ContainsKey("LanOnly"))      obj["LanOnly"]      = false;
        if (!obj.ContainsKey("SaveSlot"))     obj["SaveSlot"]     = 1;
        if (!obj.ContainsKey("SaveMode"))     obj["SaveMode"]     = "Continue";
        if (!obj.ContainsKey("SaveInterval")) obj["SaveInterval"] = 600;
        // The rest of the game's own default file, added only where missing so a hand-edited value is never touched.
        // SkipNetworkAccessibilityTest defaults to true: the test calls Steam's HTTP API, and on a host where that call
        // fails the server hangs there instead of starting (seen on a test server, 29.10.2026). It only checks reachability.
        if (!obj.ContainsKey("IdleDayCycleSpeed"))            obj["IdleDayCycleSpeed"]            = 0.0;
        if (!obj.ContainsKey("IdleTargetFramerate"))          obj["IdleTargetFramerate"]          = 5;
        if (!obj.ContainsKey("ActiveTargetFramerate"))        obj["ActiveTargetFramerate"]        = 60;
        if (!obj.ContainsKey("LogFilesEnabled"))              obj["LogFilesEnabled"]              = false;
        if (!obj.ContainsKey("TimestampLogFilenames"))        obj["TimestampLogFilenames"]        = true;
        if (!obj.ContainsKey("TimestampLogEntries"))          obj["TimestampLogEntries"]          = true;
        if (!obj.ContainsKey("SkipNetworkAccessibilityTest")) obj["SkipNetworkAccessibilityTest"] = true;
        if (!obj.ContainsKey("CustomGameModeSettings"))       obj["CustomGameModeSettings"]       = new JsonObject();

        // Merge GameSettings sub-object without overwriting existing keys
        if (obj["GameSettings"] is not JsonObject gs) { gs = new JsonObject(); obj["GameSettings"] = gs; }
        if (!gs.ContainsKey("Gameplay.TreeRegrowth")) gs["Gameplay.TreeRegrowth"] = treeRegen;
        if (!gs.ContainsKey("Structure.Damage"))      gs["Structure.Damage"]      = true;

        await File.WriteAllTextAsync(cfgPath,
            obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["GameDifficulty"]      = "Normal",
        ["RegenerationEnabled"] = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "GameDifficulty",      Label = "Pelitaso",    FieldType = ConfigFieldType.Dropdown, DefaultValue = "Normal", Options = ["Peaceful","Normal","Hard","HardSurvival"] },
            new() { Key = "RegenerationEnabled", Label = "Regeneraatio", FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
        ]);
        return fields;
    }
}
