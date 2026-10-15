using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WGS.Models;

namespace WGS.Games;

public class EnshroudedPlugin : GamePluginBase, IA2SQueryPlugin
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

    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
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
        ApplyPassword(obj, server.ServerPassword);
        if (!obj.ContainsKey("saveDirectory")) obj["saveDirectory"] = "./savegame";
        if (!obj.ContainsKey("logDirectory"))  obj["logDirectory"]  = "./logs";
        if (!obj.ContainsKey("ip"))            obj["ip"]            = "0.0.0.0";

        await File.WriteAllTextAsync(cfgPath,
            obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// The server password lives in the "Default" user group of <c>userGroups</c> — the server ignores the old top-level
    /// "password" key, which is where WGS used to write it (so the GUI password never applied and the server kept the
    /// random one from its generated config). The value comes from the GUI's Password field (server.ServerPassword).
    /// An empty GUI password leaves the file's current password alone, so one set by hand in the JSON isn't wiped.
    /// </summary>
    internal static void ApplyPassword(JsonObject obj, string? password)
    {
        if (obj["userGroups"] is not JsonArray groups) { groups = new JsonArray(); obj["userGroups"] = groups; }

        var target = groups.OfType<JsonObject>().FirstOrDefault(g =>
                         string.Equals((string?)g["name"], "Default", StringComparison.OrdinalIgnoreCase))
                     ?? groups.OfType<JsonObject>().FirstOrDefault();
        if (target == null)
        {
            target = new JsonObject
            {
                ["name"] = "Default", ["password"] = "", ["canKickBan"] = false, ["canAccessInventories"] = true,
                ["canEditWorld"] = true, ["canEditBase"] = true, ["canExtendBase"] = true, ["reservedSlots"] = 0,
            };
            groups.Add(target);
        }
        if (!string.IsNullOrEmpty(password)) target["password"] = password;
    }

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
