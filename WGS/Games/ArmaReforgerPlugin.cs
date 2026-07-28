using System.IO;
using WGS.Models;

namespace WGS.Games;

public class ArmaReforgerPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
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
    public override int    DefaultQueryPort => 17777;
    public override int    DefaultMaxPlayers => 16;
    public override bool   HasRcon          => true;
    public override string EngineFamily     => "battleye";

    // A2S query — Reforger exposes Steam Query on -a2sPort instead of RCON
    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort;

    public override string? GetPlayersCommand()               => "players";
    public override string? GetBroadcastCommand(string msg)   => $"say -1 {msg}";
    // Arma Reforger RCON kick takes the player UID
    public override string? GetKickCommand(string target)              => $"kick {target}";
    public override string? GetKickCommand(string target, string reason) => $"kick {target}";
    public override string? GetBanCommand(string target)               => $"ban {target}";
    public override string? GetBanCommand(string target, string reason) => $"ban {target}";

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

        var rconPort = server.RconPort > 0 ? server.RconPort : 19999;
        var json = $$"""
{
  "bindPort": {{server.ServerPort}},
  "publicPort": {{server.ServerPort}},
  "a2s": {
    "address": "0.0.0.0",
    "port": {{server.QueryPort}}
  },
  "rcon": {
    "address": "0.0.0.0",
    "port": {{rconPort}},
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
            new() { Key = "scenarioId",    Label = "Scenario ID",      FieldType = ConfigFieldType.Text,     DefaultValue = "{ECC61978EDCC2B5A}Missions/23_Campaign.conf",
                    Description = "Scenario to load on startup (from the server config)." },
            new() { Key = "adminPassword", Label = "Admin / RCON password", FieldType = ConfigFieldType.Password, DefaultValue = "",
                    Description = "Used for both in-game admin login and BattlEye RCON. Also set as the RCON password in WGS so the RCON tab can connect." },
            new() { Key = "configFile",    Label = "Config file",      FieldType = ConfigFieldType.Text,     DefaultValue = "serverConfig.json",
                    Description = "Relative path to serverConfig.json inside the install folder." },
        ]);
        return fields;
    }
}
