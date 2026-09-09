using System.IO;
using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class OpenRCT2Plugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId          => "openrct2";
    public override string GameName        => "OpenRCT2";
    public override string Description     => "⚠ Requires your own RollerCoaster Tycoon 2 game data + a save/scenario file BEFORE starting (Settings) — open-source engine remake of RollerCoaster Tycoon 2 with online multiplayer";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 0; // free, open source — downloaded directly from GitHub releases
    public override string Executable      => "OpenRCT2.exe";
    public override int    DefaultPort     => 11753;
    public override int    DefaultQueryPort => 11753;
    public override int    DefaultMaxPlayers => 32;

    public override async Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
    {
        try
        {
            var json = await _http.GetStringAsync("https://api.github.com/repos/OpenRCT2/OpenRCT2/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString();
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains("windows-portable-x64", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip"))
                    return (tag ?? "unknown", asset.GetProperty("browser_download_url").GetString()!);
            }
            return null;
        }
        catch { return null; }
    }

    // OpenRCT2 is only the engine — it requires the original RollerCoaster Tycoon 2 (or RCT1) game
    // data files (copyrighted, not redistributable) placed in its data folder, plus a save or
    // scenario file to host. Neither can be automated by WGS; the user supplies both manually.
    public override string? ValidateBeforeStart(GameServer server)
        => string.IsNullOrWhiteSpace(S(server, "savePath", ""))
            ? "OpenRCT2 needs a save (.sv6) or scenario (.sc6) file to host, and the original RollerCoaster Tycoon 2 game data placed in its data folder — neither can be installed automatically. Set the save/scenario path in Settings."
            : null;

    public override string BuildStartArguments(GameServer s)
    {
        var save = S(s, "savePath", "");
        var pass = s.ServerPassword ?? "";
        var passArg = string.IsNullOrWhiteSpace(pass) ? "" : $"--password \"{pass}\"";
        return $"host \"{save}\" --port {s.ServerPort} --headless {passArg}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["savePath"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "savePath", Label = "Save / scenario file (.sv6 / .sc6)", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "Full path to a save or scenario file. Requires the original RollerCoaster Tycoon 2 game data to be placed in OpenRCT2's data folder — WGS cannot provide these copyrighted files." },
        ]);
        return fields;
    }
}
