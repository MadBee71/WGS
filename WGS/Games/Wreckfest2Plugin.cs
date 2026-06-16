using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using WGS.Models;

namespace WGS.Games;

public class Wreckfest2Plugin : GamePluginBase
{
    public override string GameId        => "wreckfest2";
    public override string GameName      => "Wreckfest 2";
    public override string Description   => "Next-generation demolition derby and contact racing";
    public override string Category      => "Racing";
    public override int    SteamAppId    => 3519390;
    public override string Executable    => "Wreckfest2.exe";
    public override bool   RequiresSteamLogin    => true;
    public override bool   UseNativeConsole      => true;
    public override int    SteamClientAppId   => 0;
    public override int    DefaultPort        => 30100;
    public override int    DefaultQueryPort   => 27015;
    public override int    DefaultMaxPlayers  => 16;

    // Wreckfest 2's argument parser cannot handle spaces in --save-dir, even with quotes.
    // Use a sanitized path (spaces replaced with underscores) for both the save dir and the config.
    private static string SavePath(GameServer s)
        => s.InstallPath.TrimEnd('\\', '/').Replace(" ", "_");

    public override string BuildStartArguments(GameServer s)
        => $"--server --save-dir={SavePath(s)}";

    public override string? GetStopCommand(GameServer server) => "shutdown";

    // Write server_config.scnf and server_privilege.sprv before start
    public override async Task PreStartAsync(GameServer server)
    {
        var laps        = int.TryParse(S(server, "laps", "3"), out var l) ? l : 3;
        var gameMode    = S(server, "gameModeId", "bangerrace");
        var levelId     = S(server, "levelId", "track02_1");
        var weatherPath = S(server, "weatherPath", "data/property/weather/sandpit_cloudy_2_b.weat");
        var password    = S(server, "password", "");
        var damageId    = S(server, "vehicleDamageId", "realistic");
        var botCount    = int.TryParse(S(server, "botCount", "0"), out var b) ? b : 0;
        var savePath    = SavePath(server);

        var json = $$"""
{
  "bbmeta": "scnf v0 n1",
  "net server config": [
    {
      "bbmeta": "ncnf v1 n1",
      "name": "{{server.ServerName}}",
      "description": "Dedicated server managed by WGS",
      "password": "{{password}}",
      "game port": {{server.ServerPort}}
    }
  ],
  "game server config": [
    {
      "bbmeta": "gcnf v0 n1",
      "event rotation name": "",
      "default event": [
        {
          "bbmeta": "ecnf v0 n1",
          "level": [
            {
              "bbmeta": "mlvl v1 n1",
              "level id": "{{levelId}}",
              "weather path": "{{weatherPath}}",
              "ai set path": "career/ai-class-all.aist",
              "game mode id": "{{gameMode}}"
            }
          ],
          "rules": [
            {
              "bbmeta": "evru v4 n1",
              "laps": {{laps}},
              "time limit": 3,
              "number of teams": 2,
              "max number of participants": {{server.MaxPlayers}},
              "flags": "",
              "car reset delay (seconds)": 1,
              "vehicle damage id": "{{damageId}}"
            }
          ],
          "bot count": {{botCount}}
        }
      ],
      "countdown time": 100000,
      "flags": "idle kicker disabled"
    }
  ]
}
""";

        Directory.CreateDirectory(savePath);
        var cfgPath = Path.Combine(savePath, "server_config.scnf");

        // Merge WGS fields into existing config — preserves user customizations
        JsonObject root = new();
        if (File.Exists(cfgPath))
        {
            try { root = JsonNode.Parse(await File.ReadAllTextAsync(cfgPath))?.AsObject() ?? new(); }
            catch { }
        }

        if (root.Count == 0)
        {
            // First run — write the full default config
            await File.WriteAllTextAsync(cfgPath, json);
        }
        else
        {
            // Update only WGS-managed fields in nested structure
            var net = root["net server config"]?.AsArray()?[0]?.AsObject();
            if (net != null)
            {
                net["name"]      = server.ServerName;
                net["password"]  = password;
                net["game port"] = server.ServerPort;
            }
            var evt   = root["game server config"]?.AsArray()?[0]?.AsObject()?["default event"]?.AsArray()?[0]?.AsObject();
            var level = evt?["level"]?.AsArray()?[0]?.AsObject();
            var rules = evt?["rules"]?.AsArray()?[0]?.AsObject();
            if (level != null) { level["level id"] = levelId; level["weather path"] = weatherPath; level["game mode id"] = gameMode; }
            if (rules != null) { rules["laps"] = laps; rules["max number of participants"] = server.MaxPlayers; rules["vehicle damage id"] = damageId; }
            if (evt   != null) evt["bot count"] = botCount;

            await File.WriteAllTextAsync(cfgPath,
                root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["laps"]            = "3",
        ["gameModeId"]      = "bangerrace",
        ["levelId"]         = "track02_1",
        ["weatherPath"]     = "data/property/weather/sandpit_cloudy_2_b.weat",
        ["password"]        = "",
        ["vehicleDamageId"] = "realistic",
        ["botCount"]        = "0",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "laps",            Label = "Lap count",      FieldType = ConfigFieldType.Slider,   DefaultValue = "3", Min = 1, Max = 20 },
            new() { Key = "gameModeId",      Label = "Game mode",      FieldType = ConfigFieldType.Dropdown, DefaultValue = "bangerrace",
                    Options = ["bangerrace", "deathmatch"] },
            new() { Key = "vehicleDamageId", Label = "Vehicle damage", FieldType = ConfigFieldType.Dropdown, DefaultValue = "realistic",
                    Options = ["realistic", "normal", "hard", "easy"] },
            new() { Key = "levelId",         Label = "Default map",    FieldType = ConfigFieldType.Text,     DefaultValue = "track02_1" },
            new() { Key = "weatherPath",     Label = "Weather path",   FieldType = ConfigFieldType.Text,     DefaultValue = "data/property/weather/sandpit_cloudy_2_b.weat" },
            new() { Key = "password",        Label = "Server password",FieldType = ConfigFieldType.Text,     DefaultValue = "" },
            new() { Key = "botCount",        Label = "Bot count",      FieldType = ConfigFieldType.Slider,   DefaultValue = "0", Min = 0, Max = 24 },
        ]);
        return fields;
    }
}
