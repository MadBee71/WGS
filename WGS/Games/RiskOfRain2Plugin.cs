using System.IO;
using WGS.Models;

namespace WGS.Games;

public class RiskOfRain2Plugin : GamePluginBase
{
    public override string GameId          => "riskofrain2";
    public override string GameName        => "Risk of Rain 2";
    public override string Description     => "Co-op roguelike third-person shooter — settings are written to a startup config, not passed as CLI flags";
    public override string Category        => "Co-op";
    public override int    SteamAppId      => 1180760;
    public override int    GameStoreAppId  => 632360;
    public override string Executable      => "Risk of Rain 2.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 8;

    public override Task PreStartAsync(GameServer s)
    {
        var bootCfgPath = Path.Combine(s.InstallPath, "Risk of Rain 2_Data", "boot.cfg");
        if (File.Exists(bootCfgPath))
        {
            var lines = File.ReadAllLines(bootCfgPath);
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].StartsWith("headless=")) lines[i] = "headless=true";
            File.WriteAllLines(bootCfgPath, lines);
        }

        var startupCfgPath = Path.Combine(s.InstallPath, "Risk of Rain 2_Data", "Config", "server_startup.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(startupCfgPath)!);
        File.WriteAllText(startupCfgPath,
            $"""
            sv_hostname "{s.ServerName}"
            sv_maxplayers {s.MaxPlayers}
            sv_port {s.ServerPort}
            sv_password "{s.ServerPassword}"
            """);
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
