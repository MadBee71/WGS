using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using WGS.Models;

namespace WGS.Games;

public class Wreckfest2Plugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId        => "wreckfest2";
    public override string GameName      => "Wreckfest 2";
    public override string Description   => "Next-generation demolition derby and contact racing";
    public override string Category      => "Racing";
    public override int    SteamAppId    => 3519390;
    public override int    GameStoreAppId => 1203190;
    public override string Executable    => "Wreckfest2.exe";
    public override bool   RequiresSteamLogin    => true;
    public override bool   UseNativeConsole      => true;
    public override int    SteamClientAppId   => 1203190;
    public override int    DefaultPort        => 30100;
    public override int    DefaultQueryPort   => 27020;
    public override int    DefaultSteamPort   => 30200;
    public override int    DefaultMaxPlayers  => 16;

    // Save folder, always directly inside this server's own InstallPath — one per server,
    // no collision even if two Wreckfest 2 servers share a parent game folder.
    private const string SaveDirName = "Saves";
    private static string SavePath(GameServer s)
        => Path.Combine(s.InstallPath.TrimEnd('\\', '/'), SaveDirName);

    // Pre-fix versions used the PARENT of InstallPath, which every server under the same game folder
    // shares — so multiple Wreckfest 2 servers all wrote to (and overwrote) the same "Saves" folder.
    private static string LegacySharedSavePath(GameServer s)
        => Path.Combine(Path.GetDirectoryName(s.InstallPath.TrimEnd('\\', '/'))!, "Saves")
               .Replace(" ", "_");

    // One-time migration: if this server has no per-server Saves folder yet but the old shared one
    // has data, copy it in so existing config/admin list isn't lost. Never deletes the old copy.
    private static void MigrateLegacySaveDataIfNeeded(GameServer s)
    {
        var newPath = SavePath(s);
        var oldPath = LegacySharedSavePath(s);
        if (Directory.Exists(newPath) || !Directory.Exists(oldPath)) return;

        Directory.CreateDirectory(newPath);
        foreach (var file in Directory.GetFiles(oldPath))
        {
            try { File.Copy(file, Path.Combine(newPath, Path.GetFileName(file)), overwrite: false); }
            catch { }
        }
    }

    // The game always loads server_cup_config.ccnl from {InstallPath}\save\ regardless of --save-dir.
    private static string GameNativeSavePath(GameServer s)
        => Path.Combine(s.InstallPath.TrimEnd('\\', '/'), "save");


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;

    // Relative, not the absolute SavePath: the process's WorkingDirectory is always this server's
    // own InstallPath (see ServerManagerService), so a bare relative name resolves to InstallPath\Saves
    // with zero spaces in the argument itself — sidesteps Bugbear's parser choking on spaces even when
    // quoted, without requiring the install folder name to be space-free.
    public override string BuildStartArguments(GameServer s)
        => $"--server --save-dir={SaveDirName}";

    // Like Wreckfest 1, no known stdin stop command, RCON, or REST API to shut down through — Stop
    // always falls straight through to a hard kill. A GetStopCommand("shutdown") used to sit here,
    // but UseNativeConsole=true above means WGS never writes to stdin for this game (see
    // ServerManagerService.StopAsync), so it could never actually fire — dead, misleading code
    // that made Stop look graceful when it wasn't. Removed rather than left in place.

    // Write server_config.scnf and server_privilege.sprv before start
    public override async Task PreStartAsync(GameServer server)
    {
        var password    = S(server, "password", "");
        var adminId     = S(server, "adminSteamId", "");
        var laps        = int.TryParse(S(server, "laps", "3"), out var l) ? l : 3;
        var gameMode    = S(server, "gameModeId", "bangerrace");
        var levelId     = S(server, "levelId", "track02_1");
        var weatherPath = S(server, "weatherPath", "data/property/weather/sandpit_cloudy_2_b.weat");
        var damageId    = S(server, "vehicleDamageId", "realistic");
        var botCount    = int.TryParse(S(server, "botCount", "0"), out var b) ? b : 0;
        var carType     = S(server, "carType", "");
        var savePath    = SavePath(server);

        MigrateLegacySaveDataIfNeeded(server);
        Directory.CreateDirectory(savePath);

        // --- server_config.scnf ---
        var cfgPath = Path.Combine(savePath, "server_config.scnf");
        JsonObject root = new();
        if (File.Exists(cfgPath))
        {
            try { root = JsonNode.Parse(await File.ReadAllTextAsync(cfgPath))?.AsObject() ?? new(); }
            catch { }
        }

        var carRestrictionJson = string.IsNullOrWhiteSpace(carType) ? "[]" :
            $$"""
[{ "allowed car type": "{{carType}}", "flags": "" }]
""".Trim();

        if (root.Count == 0)
        {
            var json = $$"""
{
    "net server config": [
        {
            "name": "{{server.ServerName}}",
            "description": "Dedicated server managed by WGS",
            "password": "{{password}}",
            "log file name": "",
            "game port": {{server.ServerPort}}
        }
    ],
    "game server config": [
        {
            "cup list": "",
            "event loop name": "default_loop",
            "countdown time": 100000,
            "voting time": 20000,
            "flags": "idle kicker disabled",
            "auto ban report limits": [{ "censor report limit": 4, "silence report limit": 8, "suspend report limit": 12 }],
            "server ban durations":   [{ "censor duration": 1440, "silence duration": 1440, "suspend duration": 525600 }],
            "admin ban durations":    [{ "censor duration": 1440, "silence duration": 1440, "suspend duration": 525600 }],
            "default ban durations":  [{ "censor duration": 1440, "silence duration": 1440, "suspend duration": 525600 }]
        }
    ]
}
""";
            await File.WriteAllTextAsync(cfgPath, json);
        }
        else
        {
            // Update WGS-managed fields — preserves user customizations
            var net = root["net server config"]?.AsArray()?[0]?.AsObject();
            if (net != null)
            {
                net["name"]      = server.ServerName;
                net["password"]  = password;
                net["game port"] = server.ServerPort;
            }
            var gs = root["game server config"]?.AsArray()?[0]?.AsObject();
            if (gs != null)
            {
                gs["event loop name"] = "default_loop";
                gs.Remove("default event");
            }
            await File.WriteAllTextAsync(cfgPath,
                root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        // --- server_privilege.sprv (only if admin SteamID configured, only on first run) ---
        if (!string.IsNullOrWhiteSpace(adminId) && long.TryParse(adminId, out var steamId))
        {
            var prvPath = Path.Combine(savePath, "server_privilege.sprv");
            if (!File.Exists(prvPath))
            {
                var sprv = $$"""
{
    "bbmeta": "sprv v0 n1",
    "roles": [
        {
            "bbmeta": "ssrl v0 n1",
            "platform id": {{steamId}},
            "role": "admin"
        }
    ],
    "bans": []
}
""";
                await File.WriteAllTextAsync(prvPath, sprv);
            }
        }

        // --- server_cup_config.ccnl (game loads this from {InstallPath}\save\, not from --save-dir) ---
        var nativeSavePath = GameNativeSavePath(server);
        Directory.CreateDirectory(nativeSavePath);
        var cupPath = Path.Combine(nativeSavePath, "server_cup_config.ccnl");
        if (!File.Exists(cupPath))
        {
            var carRestrCup = string.IsNullOrWhiteSpace(carType) ? "[]" :
                $"[{{\"allowed car type\":\"{carType}\",\"flags\":\"\"}}]";
            var cup = $$"""
{
    "cups": [
        {
            "race settings": [
                {
                    "point distribution": "30;27;25;20;19;18;17;16;15;14;13;12;11;10;9;8;7;6;5;4;3;2;1;0",
                    "flags": "",
                    "start order": "random",
                    "points for best lap time": 0,
                    "points for highest score": 0,
                    "last race multiplier": 1
                }
            ],
            "event loop": "",
            "cup name": "{{server.ServerName}}",
            "cup description": "Managed by WGS",
            "icon": "",
            "car restriction": {{carRestrCup}}
        }
    ]
}
""";
            await File.WriteAllTextAsync(cupPath, cup);
        }
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["password"]        = "",
        ["adminSteamId"]    = "",
        ["laps"]            = "3",
        ["gameModeId"]      = "bangerrace",
        ["levelId"]         = "track02_1",
        ["weatherPath"]     = "data/property/weather/sandpit_cloudy_2_b.weat",
        ["vehicleDamageId"] = "realistic",
        ["botCount"]        = "0",
        ["carType"]         = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "password",        Label = "Server password", FieldType = ConfigFieldType.Text,     DefaultValue = "" },
            new() { Key = "adminSteamId",    Label = "Admin Steam ID",  FieldType = ConfigFieldType.Text,     DefaultValue = "" },
            new() { Key = "laps",            Label = "Lap count",       FieldType = ConfigFieldType.Slider,   DefaultValue = "3",  Min = 1, Max = 20 },
            new() { Key = "gameModeId",      Label = "Game mode",       FieldType = ConfigFieldType.Dropdown, DefaultValue = "bangerrace",
                    Options = ["bangerrace", "deathmatch", "suiciderace"] },
            new() { Key = "vehicleDamageId", Label = "Vehicle damage",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "realistic",
                    Options = ["realistic", "normal", "hard", "easy"] },
            new() { Key = "carType",         Label = "Car restriction",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "",
                    Options = ["", "car01", "car02", "car03", "car04", "car05", "car05_half", "car06", "car07", "car08",
                               "car09", "car10", "car11", "car12", "car13", "car14", "motorhome", "school_bus", "car15", "car16"] },
            new() { Key = "levelId",         Label = "Default map",     FieldType = ConfigFieldType.Text,     DefaultValue = "track02_1" },
            new() { Key = "weatherPath",     Label = "Weather path",    FieldType = ConfigFieldType.Text,     DefaultValue = "data/property/weather/sandpit_cloudy_2_b.weat" },
            new() { Key = "botCount",        Label = "Bot count",       FieldType = ConfigFieldType.Slider,   DefaultValue = "0", Min = 0, Max = 24 },
        ]);
        return fields;
    }
}
