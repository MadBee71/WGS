using System.IO;
using WGS.Models;

namespace WGS.Games;

public class TheForestPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId        => "theforest";
    public override string GameName      => "The Forest";
    public override string Description   => "Survival horror on a cannibal island";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 556450;
    public override int    GameStoreAppId => 242760;
    public override int    WorkshopAppId   => 556450;

    public string ModTargetDirectory => "Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable    => "TheForestDedicatedServer.exe";
    public override int    DefaultPort      => 27017;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 64;
    public override bool   HasRcon            => true;
    public override bool   RequiresSteamLogin => true;
    protected override bool FilterUnityShaderNoise => true;


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;

    /// <summary>The game reads and rewrites Server.cfg in the user data folder (shared by every instance on the Windows user);
    /// listed (absolute path) so the Config tab edits it.</summary>
    public static string UserDataConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "SKS", "TheForestDedicatedServer", "ds", "Server.cfg");
    public override List<string> ConfigFiles => [UserDataConfigPath];

    // Applies the GUI settings to Server.cfg ("key value" lines) and leaves every other line, comments included, as it is.
    public override async Task PreStartAsync(GameServer server)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UserDataConfigPath)!);
        var lines = File.Exists(UserDataConfigPath)
            ? (await File.ReadAllLinesAsync(UserDataConfigPath)).ToList()
            : [];

        // A bind address already in the file is kept unless the Settings tab names one (same rule as Sons of the Forest).
        var ip = server.ServerIp?.Trim();
        if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0") SetValue(lines, "serverIP", ip, true);
        else SetValue(lines, "serverIP", "0.0.0.0", false);
        SetValue(lines, "serverGamePort",  server.ServerPort.ToString(),  true);
        SetValue(lines, "serverQueryPort", server.QueryPort.ToString(),   true);
        SetValue(lines, "serverName",      server.ServerName,             true);
        SetValue(lines, "serverPlayers",   server.MaxPlayers.ToString(),  true);
        // An empty GUI password leaves one set by hand in the file alone.
        if (!string.IsNullOrEmpty(server.ServerPassword)) SetValue(lines, "serverPassword", server.ServerPassword, true);
        else SetValue(lines, "serverPassword", "", false);
        SetValue(lines, "difficulty", S(server, "difficulty", "Normal"), true);
        SetValue(lines, "veganMode",  S(server, "vegan", "false") == "true" ? "on" : "off", true);

        // The game's own defaults, added only where missing.
        SetValue(lines, "serverSteamPort", "8766", false);
        SetValue(lines, "enableVAC", "on", false);
        SetValue(lines, "serverAutoSaveInterval", "30", false);
        SetValue(lines, "initType", "Continue", false);
        SetValue(lines, "slot", "1", false);
        SetValue(lines, "showLogs", "off", false);
        SetValue(lines, "targetFpsIdle", "5", false);
        SetValue(lines, "targetFpsActive", "60", false);

        await File.WriteAllLinesAsync(UserDataConfigPath, lines);
    }

    private static void SetValue(List<string> lines, string key, string value, bool overwrite)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("//")) continue;
            var parts = t.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            if (overwrite) lines[i] = $"{key} {value}";
            return;
        }
        lines.Add($"{key} {value}");
    }

    public override string BuildStartArguments(GameServer s)
    {
        var difficulty = S(s, "difficulty", "Normal");
        var caves      = S(s, "caves",    "true")  == "true" ? "1" : "0";
        var enemies    = S(s, "enemies",  "true")  == "true" ? "1" : "0";
        var vegan      = S(s, "vegan",    "false") == "true" ? "1" : "0";
        return $"-batchmode -nographics serverName \"{s.ServerName}\" serverPlayers {s.MaxPlayers} " +
               $"serverPort {s.ServerPort} serverQueryPort {s.QueryPort} " +
               $"serverPassword \"{s.ServerPassword}\" difficulty {difficulty} " +
               $"serverCaves {caves} serverEnemies {enemies} serverVegan {vegan}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["difficulty"] = "Normal",
        ["caves"]      = "true",
        ["enemies"]    = "true",
        ["vegan"]      = "false",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "difficulty",   Label = "Vaikeustaso",   FieldType = ConfigFieldType.Dropdown, DefaultValue = "Normal", Options = ["Peaceful","Normal","Hard","HardSurvival"] },
            new() { Key = "caves",        Label = "Luolat",        FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
            new() { Key = "enemies",      Label = "Viholliset",    FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
            new() { Key = "vegan",        Label = "Vegan mode",    FieldType = ConfigFieldType.Toggle,   DefaultValue = "false" },
        ]);
        return fields;
    }
}
