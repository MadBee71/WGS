using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WGS.Models;

namespace WGS.Games;

public class EnshroudedPlugin : GamePluginBase
{
    public override string GameId          => "enshrouded";
    public override string GameName        => "Enshrouded";
    public override string Description     => "Survival crafting in a fog-shrouded open world";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2278520;
    public override int    GameStoreAppId  => 1203620;
    public override string Executable      => "enshrouded_server.exe";
    public override int    DefaultPort     => 15636;
    public override int    DefaultQueryPort => 15637;
    public override int    DefaultMaxPlayers => 16;

    // Enshrouded reads all config from enshrouded_server.json — no CLI arguments
    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override async Task PreStartAsync(GameServer server)
    {
        var cfgPath = Path.Combine(server.InstallPath, "enshrouded_server.json");

        // Merge WGS-managed fields into existing config to preserve user customizations
        JsonObject obj = new();
        if (File.Exists(cfgPath))
        {
            try { obj = JsonNode.Parse(await File.ReadAllTextAsync(cfgPath))?.AsObject() ?? new(); }
            catch { obj = new(); }
        }

        obj["name"]          = server.ServerName;
        obj["gamePort"]      = server.ServerPort;
        obj["queryPort"]     = server.QueryPort;
        obj["slotCount"]     = server.MaxPlayers;
        if (!obj.ContainsKey("password"))     obj["password"]      = S(server, "serverPass");
        if (!obj.ContainsKey("saveDirectory")) obj["saveDirectory"] = "./savegame";
        if (!obj.ContainsKey("logDirectory"))  obj["logDirectory"]  = "./logs";
        if (!obj.ContainsKey("ip"))            obj["ip"]            = "0.0.0.0";

        await File.WriteAllTextAsync(cfgPath,
            obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
