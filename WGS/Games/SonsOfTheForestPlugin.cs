using System.IO;
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

    // Writes dedicatedserver.cfg every start so WGS settings are always applied
    public override async Task PreStartAsync(GameServer server)
    {
        var safeName  = server.ServerName.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var password  = S(server, "serverPass",  "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        var gameMode  = S(server, "GameDifficulty", "Normal");
        var treeRegen = S(server, "RegenerationEnabled", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        var maxPlayers = Math.Min(server.MaxPlayers, 8); // game hard cap

        var json = $$"""
{
  "IpAddress": "0.0.0.0",
  "GamePort": {{server.ServerPort}},
  "QueryPort": {{server.QueryPort}},
  "BlobSyncPort": 9700,
  "ServerName": "{{safeName}}",
  "MaxPlayers": {{maxPlayers}},
  "Password": "{{password}}",
  "LanOnly": false,
  "SaveSlot": 1,
  "SaveMode": "Continue",
  "GameMode": "{{gameMode}}",
  "GameSettings": {
    "Gameplay.TreeRegrowth": {{(treeRegen ? "true" : "false")}},
    "Structure.Damage": true
  },
  "SaveInterval": 600,
  "IdleDayCycleSpeed": 0.0,
  "IdleTargetFramerate": 5,
  "ActiveTargetFramerate": 60,
  "LogFilesEnabled": false,
  "SkipNetworkAccessibilityTest": false
}
""";
        await File.WriteAllTextAsync(
            Path.Combine(server.InstallPath, "dedicatedserver.cfg"), json);
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
