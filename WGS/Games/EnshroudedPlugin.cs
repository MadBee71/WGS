using System.IO;
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
        var json = $$"""
{
  "name": "{{server.ServerName}}",
  "password": "{{S(server, "serverPass")}}",
  "saveDirectory": "./savegame",
  "logDirectory": "./logs",
  "ip": "0.0.0.0",
  "gamePort": {{server.ServerPort}},
  "queryPort": {{server.QueryPort}},
  "slotCount": {{server.MaxPlayers}}
}
""";
        await File.WriteAllTextAsync(
            Path.Combine(server.InstallPath, "enshrouded_server.json"), json);
    }

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
