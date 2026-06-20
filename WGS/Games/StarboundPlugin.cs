using System.IO;
using System.Text.RegularExpressions;
using WGS.Models;

namespace WGS.Games;

public class StarboundPlugin : GamePluginBase
{
    public override string GameId          => "starbound";
    public override string GameName        => "Starbound";
    public override string Description     => "2D sandbox space exploration RPG";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 533830;
    public override string Executable      => "win64/starbound_server.exe";
    public override int    DefaultPort      => 21025;
    public override int    DefaultQueryPort => 21025;
    public override int    DefaultMaxPlayers => 8;

    public override Task PreStartAsync(GameServer s)
    {
        Directory.CreateDirectory(Path.Combine(s.InstallPath, "storage"));
        var configPath = Path.Combine(s.InstallPath, "storage", "starbound_server.config");
        WriteConfigIfMissing(configPath,
            $$"""
            {
              "allowAdminCommands" : true,
              "allowAnonymousConnections" : true,
              "gameServerBind" : "*",
              "gameServerPort" : {{s.ServerPort}},
              "maxPlayers" : {{s.MaxPlayers}},
              "queryServerBind" : "*",
              "queryServerPort" : {{s.QueryPort}},
              "serverName" : "{{s.ServerName}}"
            }
            """);

        if (File.Exists(configPath))
        {
            var content = File.ReadAllText(configPath);
            content = Regex.Replace(content, "\"gameServerPort\" *: *[0-9]+", $"\"gameServerPort\" : {s.ServerPort}");
            content = Regex.Replace(content, "\"queryServerPort\" *: *[0-9]+", $"\"queryServerPort\" : {s.QueryPort}");
            content = Regex.Replace(content, "\"maxPlayers\" *: *[0-9]+", $"\"maxPlayers\" : {s.MaxPlayers}");
            File.WriteAllText(configPath, content);
        }
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
