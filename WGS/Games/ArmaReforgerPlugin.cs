using System.IO;
using WGS.Models;

namespace WGS.Games;

public class ArmaReforgerPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId        => "armareforger";
    public override string GameName      => "Arma Reforger";
    public override string Description   => "Military sandbox with mod support via Bohemia Workshop";
    public override string Category      => "Military";
    public override int    SteamAppId    => 1874900;
    public override int    GameStoreAppId => 1874880;
    public override int    WorkshopAppId  => 1874880;
    public override string Executable    => "ArmaReforgerServer.exe";

    public string ModTargetDirectory => string.Empty;
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupAHelper.OnModDownloadedAsync(s, w, id);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupAHelper.OnModRemovedAsync(s, w, id);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _)      => GroupAHelper.BuildModArguments(ids);
    public override int    DefaultPort      => 2302;
    public override int    DefaultQueryPort => 7777;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
    {
        var cfg = S(s, "configFile", "serverConfig.json");
        return $"-config \"{s.InstallPath}\\{cfg}\" -bindPort {s.ServerPort} -a2sPort {s.QueryPort} -maxFPS 60";
    }

    // Creates a minimal serverConfig.json on first run if one doesn't exist.
    // If the user has already customised it, we leave it untouched.
    public override async Task PreStartAsync(GameServer server)
    {
        var cfgRelative = S(server, "configFile", "serverConfig.json");
        var cfgPath     = Path.Combine(server.InstallPath, cfgRelative);
        if (File.Exists(cfgPath)) return;

        var dir = Path.GetDirectoryName(cfgPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var safeName   = server.ServerName.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var pass       = S(server, "serverPass",   "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        var adminPass  = S(server, "adminPassword","").Replace("\\", "\\\\").Replace("\"", "\\\"");
        var scenarioId = S(server, "scenarioId", "{ECC61978EDCC2B5A}Missions/23_Campaign.conf");

        var json = $$"""
{
  "bindPort": {{server.ServerPort}},
  "publicPort": {{server.ServerPort}},
  "a2s": {
    "address": "0.0.0.0",
    "port": {{server.QueryPort}}
  },
  "rcon": {
    "address": "127.0.0.1",
    "port": 19999,
    "password": "{{adminPass}}",
    "permission": "admin"
  },
  "game": {
    "name": "{{safeName}}",
    "password": "{{pass}}",
    "passwordAdmin": "{{adminPass}}",
    "maxPlayers": {{server.MaxPlayers}},
    "visible": true,
    "crossPlatform": false,
    "scenarioId": "{{scenarioId}}",
    "gameProperties": {
      "fastValidation": true,
      "battlEye": true
    },
    "mods": []
  }
}
""";
        await File.WriteAllTextAsync(cfgPath, json);
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["configFile"]    = "serverConfig.json",
        ["scenarioId"]    = "{ECC61978EDCC2B5A}Missions/23_Campaign.conf",
        ["adminPassword"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "scenarioId",    Label = "Scenario ID",      FieldType = ConfigFieldType.Text,     DefaultValue = "{ECC61978EDCC2B5A}Missions/23_Campaign.conf" },
            new() { Key = "adminPassword", Label = "Admin password",   FieldType = ConfigFieldType.Password, DefaultValue = "" },
            new() { Key = "configFile",    Label = "Config file",      FieldType = ConfigFieldType.Text,     DefaultValue = "serverConfig.json" },
        ]);
        return fields;
    }
}
