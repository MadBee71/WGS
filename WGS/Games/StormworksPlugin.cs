using System.IO;
using System.Text.RegularExpressions;
using WGS.Models;

namespace WGS.Games;

public class StormworksPlugin : GamePluginBase
{
    public override string GameId          => "stormworks";
    public override string GameName        => "Stormworks: Build and Rescue";
    public override string Description     => "Vehicle-building rescue simulation — game and query share a single port";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 1247090;
    public override int    GameStoreAppId  => 573090;
    public override string Executable      => "server.exe";
    public override int    DefaultPort      => 25564;
    public override int    DefaultQueryPort => 25564;
    public override int    DefaultMaxPlayers => 8;

    public override Task PreStartAsync(GameServer s)
    {
        var configPath = Path.Combine(s.InstallPath, "rom", "data", "server_config.xml");
        if (File.Exists(configPath))
        {
            var content = File.ReadAllText(configPath);
            content = Regex.Replace(content, "port=\"[0-9]+\"", $"port=\"{s.ServerPort}\"");
            File.WriteAllText(configPath, content);
        }
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
