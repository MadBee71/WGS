using System.IO;
using Newtonsoft.Json.Linq;
using WGS.Models;

namespace WGS.Games;

public class MotorTownBTWPlugin : GamePluginBase
{
    public override string GameId          => "motortownbtw";
    public override string GameName        => "Motor Town: Behind The Wheel";
    public override string Description     => "Open-world trucking and driving simulator";
    public override string Category        => "Racing";
    public override int    SteamAppId      => 2223650;
    public override string SteamBranch     => "test";
    public override string Executable      => @"MotorTown\Binaries\Win64\MotorTownServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 200;

    public override Task PreStartAsync(GameServer s)
    {
        var configPath = Path.Combine(s.InstallPath, "DedicatedServerConfig.json");
        var samplePath = Path.Combine(s.InstallPath, "DedicatedServerConfig_Sample.json");
        if (!File.Exists(configPath) && File.Exists(samplePath))
            File.Copy(samplePath, configPath);

        if (File.Exists(configPath))
        {
            var config = JObject.Parse(File.ReadAllText(configPath));
            config["ServerName"] = s.ServerName;
            config["MaxPlayers"] = s.MaxPlayers;
            File.WriteAllText(configPath, config.ToString());
        }
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
        => $"Jeju_World?listen -server -MultiHome={s.ServerIp} -Port={s.ServerPort} -QueryPort={s.QueryPort} -log -useperfthreads";

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
