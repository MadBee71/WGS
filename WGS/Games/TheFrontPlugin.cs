using System;
using System.IO;
using WGS.Models;

namespace WGS.Games;

public class TheFrontPlugin : GamePluginBase
{
    public override string GameId          => "thefront";
    public override string GameName        => "The Front";
    public override string Description     => "Open-world survival crafting game";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2612550;
    public override string Executable      => @"ProjectWar\Binaries\Win64\TheFrontServer.exe";
    public override int    DefaultPort      => 27047;
    public override int    DefaultQueryPort => 27048;
    public override int    DefaultMaxPlayers => 100;

    public override Task PreStartAsync(GameServer s)
    {
        var configServerName = new Random().Next(100000000, 999999999).ToString();
        var config =
            $"[BaseServerConfig]\r\nServerName={s.ServerName}\r\nServerPassword=\r\nPort={s.ServerPort}\r\nQueryPort={s.QueryPort}\r\nSaveWorldInterval=300\r\n";
        var dir = Path.Combine(s.InstallPath, "TheFrontManager");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"ServerConfig_{configServerName}.ini"), config);
        s.GameSpecificSettings["configServerName"] = configServerName;
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var configServerName = s.GameSpecificSettings.TryGetValue("configServerName", out var n) ? n : "1";
        return $"ProjectWar_Start?DedicatedServer?MaxPlayers={s.MaxPlayers}?udrs=steam -server -game -log " +
               $"-UserDir=\"{s.InstallPath}\" -OutIPAddress={s.ServerIp} -ServerName=\"{s.ServerName}\" " +
               $"-Port=\"{s.ServerPort}\" -QueryPort=\"{s.QueryPort}\" -ConfigServerName=\"{configServerName}\"";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
