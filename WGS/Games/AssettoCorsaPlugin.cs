using System.IO;
using WGS.Models;

namespace WGS.Games;

public class AssettoCorsaPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId            => "assettocorsa";
    public override string GameName          => "Assetto Corsa";
    public override string Description       => "Racing simulation dedicated server";
    public override string Category          => "Racing";
    public override int    SteamAppId        => 302550;
    public override int    WorkshopAppId   => 244210;

    public string ModTargetDirectory => "mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModDownloadedAsync(s, w, id, n, "cfg/server_cfg.ini", "MODS");
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModRemovedAsync(s, id, "cfg/server_cfg.ini");
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override int    GameStoreAppId    => 244210;
    public override string Executable        => "acServer.exe";
    public override int    DefaultPort       => 9600;
    public override int    DefaultQueryPort  => 9601;
    public override int    DefaultMaxPlayers => 18;
    public override bool   RequiresSteamLogin => true;
    public override bool   HasRcon           => false;

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override async Task PreStartAsync(GameServer server)
    {
        var cfgDir = Path.Combine(server.InstallPath, "cfg");
        Directory.CreateDirectory(cfgDir);

        var cfgPath = Path.Combine(cfgDir, "server_cfg.ini");

        var track    = S(server, "track", "magione");
        var carList  = S(server, "cars", "ks_mazda_mx5_nd");
        var weather  = S(server, "weatherGraphics", "3_clear");

        // Read existing cfg and update key values if present, otherwise write a minimal default
        string content;
        if (File.Exists(cfgPath))
        {
            content = await File.ReadAllTextAsync(cfgPath);
            content = ReplaceIniValue(content, "SERVER", "NAME", server.ServerName);
            content = ReplaceIniValue(content, "SERVER", "TRACK", track);
            content = ReplaceIniValue(content, "SERVER", "MAX_CLIENTS", server.MaxPlayers.ToString());
            content = ReplaceIniValue(content, "SERVER", "TCP_PORT", server.ServerPort.ToString());
            content = ReplaceIniValue(content, "SERVER", "UDP_PORT", server.ServerPort.ToString());
            content = ReplaceIniValue(content, "SERVER", "HTTP_PORT", server.QueryPort.ToString());
        }
        else
        {
            content = $@"[SERVER]
NAME={server.ServerName}
CARS={carList}
CONFIG_TRACK=
TRACK={track}
MAX_CLIENTS={server.MaxPlayers}
WELCOME_MESSAGE=
PASSWORD=
ADMIN_PASSWORD=
UDP_PORT={server.ServerPort}
TCP_PORT={server.ServerPort}
HTTP_PORT={server.QueryPort}
MAX_BALLAST_KG=0
QUALIFY_MAX_WAIT_PERC=120
RACE_PIT_WINDOW_START=0
RACE_PIT_WINDOW_END=0
REVERSED_GRID_RACE_POSITIONS=0
LOCKED_ENTRY_LIST=0
PICKUP_MODE_ENABLED=1
SLEEP_TIME=1
VOTING_QUORUM=75
VOTE_DURATION=20
BLACKLIST_MODE=0
FUEL_RATE=100
DAMAGE_MULTIPLIER=100
TYRE_WEAR_RATE=100
ALLOWED_TYRES_OUT=2
ABS_ALLOWED=1
TC_ALLOWED=1
STABILITY_ALLOWED=0
AUTOCLUTCH_ALLOWED=0
TYRE_BLANKETS_ALLOWED=0
FORCE_VIRTUAL_MIRROR=0
UDP_PLUGIN_LOCAL_PORT=0
UDP_PLUGIN_ADDRESS=
AUTH_PLUGIN_ADDRESS=
LEGAL_TYRES=
RACE_GAS_PENALTY_DISABLED=0
RESULT_SCREEN_TIME=60
RACE_EXTRA_LAP=0
MAX_CONTACTS_PER_KM=0
LOOP_MODE=1
REGISTER_TO_LOBBY=1
MAX_CLIENTS_OVERRIDE=0

[FTP]
HOST=
LOGIN=
PASSWORD=
FOLDER=
LINUX=0

[PRACTICE]
NAME=Practice
TIME=30
IS_OPEN=1

[QUALIFY]
NAME=Qualify
TIME=10
IS_OPEN=1

[RACE]
NAME=Race
LAPS=5
TIME=0
WAIT_TIME=60
IS_OPEN=2

[DYNAMIC_TRACK]
SESSION_START=96
RANDOMNESS=1
LAP_GAIN=132
SESSION_TRANSFER=50

[WEATHER_0]
GRAPHICS={weather}
BASE_TEMPERATURE_AMBIENT=26
BASE_TEMPERATURE_ROAD=30
VARIATION_AMBIENT=1
VARIATION_ROAD=1
";
        }

        await File.WriteAllTextAsync(cfgPath, content);
    }

    private static string ReplaceIniValue(string content, string section, string key, string value)
    {
        // Simple line-by-line replacement within the target section
        var lines = content.Split('\n');
        var inSection = false;
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                inSection = trimmed.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase);
            else if (inSection && trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}={value}";
                break;
            }
        }
        return string.Join('\n', lines);
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["track"]         = "magione",
        ["cars"]          = "ks_mazda_mx5_nd",
        ["weatherGraphics"] = "3_clear",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "track", Label = "Track",        FieldType = ConfigFieldType.Text, DefaultValue = "magione" },
            new() { Key = "cars",  Label = "Cars (comma-separated)", FieldType = ConfigFieldType.Text, DefaultValue = "ks_mazda_mx5_nd" },
            new() { Key = "weatherGraphics", Label = "Weather", FieldType = ConfigFieldType.Dropdown, DefaultValue = "3_clear",
                Options = ["1_heavy_fog", "2_light_fog", "3_clear", "4_mid_clear", "5_hot_and_dry"] },
        ]);
        return fields;
    }
}
