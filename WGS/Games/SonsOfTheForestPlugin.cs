using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WGS.Models;

namespace WGS.Games;

public class SonsOfTheForestPlugin : GamePluginBase, IWorkshopPlugin
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

    public override string BuildStartArguments(GameServer s)
        => string.Empty; // config via dedicatedserver.cfg

    // Merges WGS-managed fields into existing config to preserve user customizations
    public override async Task PreStartAsync(GameServer server)
    {
        var cfgPath    = Path.Combine(server.InstallPath, "dedicatedserver.cfg");
        var maxPlayers = Math.Min(server.MaxPlayers, 8); // game hard cap
        var gameMode   = S(server, "GameDifficulty", "Normal");
        var treeRegen  = S(server, "RegenerationEnabled", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        JsonObject obj = new();
        if (File.Exists(cfgPath))
        {
            try { obj = JsonNode.Parse(await File.ReadAllTextAsync(cfgPath))?.AsObject() ?? new(); }
            catch { obj = new(); }
        }

        obj["IpAddress"]  = "0.0.0.0";
        obj["GamePort"]   = server.ServerPort;
        obj["QueryPort"]  = server.QueryPort;
        obj["ServerName"] = server.ServerName;
        obj["MaxPlayers"] = maxPlayers;
        obj["GameMode"]   = gameMode;
        if (!obj.ContainsKey("Password"))     obj["Password"]     = S(server, "serverPass", "");
        if (!obj.ContainsKey("BlobSyncPort")) obj["BlobSyncPort"] = 9700;
        if (!obj.ContainsKey("LanOnly"))      obj["LanOnly"]      = false;
        if (!obj.ContainsKey("SaveSlot"))     obj["SaveSlot"]     = 1;
        if (!obj.ContainsKey("SaveMode"))     obj["SaveMode"]     = "Continue";
        if (!obj.ContainsKey("SaveInterval")) obj["SaveInterval"] = 600;

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
