using System.IO;
using WGS.Models;

namespace WGS.Games;

public class EchoesOfElysiumPlugin : GamePluginBase
{
    public override string GameId          => "echoesofelysium";
    public override string GameName        => "Echoes of Elysium";
    public override string Description     => "Co-op survival crafting game";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2915100;
    public override string Executable      => "ElysiumServer.exe";
    public override int    DefaultPort      => 27017;
    public override int    DefaultQueryPort => 27017;
    public override int    DefaultMaxPlayers => 16;

    public override Task PreStartAsync(GameServer s)
    {
        var configPath = Path.Combine(s.InstallPath, "config.json");
        WriteConfigIfMissing(configPath,
            $$"""
            {
              "address": "{{s.ServerIp}}",
              "backupFreqMins": 30,
              "backupsEnabled": "true",
              "enableProfiling": "true",
              "gameDataDir": "./GameData/",
              "logsDir": "./logs/",
              "maxBackups": 5,
              "name": "{{s.ServerName}}",
              "password": "",
              "port": {{s.ServerPort}},
              "saveFreqMins": 5,
              "worldDataDir": "./world/"
            }
            """);
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
        => "--config ./config.json";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
