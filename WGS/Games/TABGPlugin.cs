using System.IO;
using WGS.Models;

namespace WGS.Games;

public class TABGPlugin : GamePluginBase
{
    public override string GameId          => "tabg";
    public override string GameName        => "Totally Accurate Battlegrounds";
    public override string Description     => "Comedic physics-based battle royale";
    public override string Category        => "Battle Royale";
    public override int    SteamAppId      => 2311970;
    public override string Executable      => "TABG.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7777;
    public override int    DefaultMaxPlayers => 16;

    public override Task PreStartAsync(GameServer s)
    {
        var configPath = Path.Combine(s.InstallPath, "game_settings.txt");
        if (!File.Exists(configPath)) return Task.CompletedTask;

        var lines = File.ReadAllLines(configPath);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("ServerName=")) lines[i] = $"ServerName={s.ServerName}";
            else if (lines[i].Contains("Port=")) lines[i] = $"Port={s.ServerPort}";
            else if (lines[i].Contains("MaxPlayers=")) lines[i] = $"MaxPlayers={s.MaxPlayers}";
        }
        File.WriteAllLines(configPath, lines);
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
