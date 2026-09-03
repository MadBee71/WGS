using System.IO;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class NuclearOptionPlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId           => "nuclearoption";
    public override string GameName         => "Nuclear Option";
    public override string Description      => "Multiplayer air combat RTS — config generated on first launch";
    public override string Category         => "Action";
    public override int    SteamAppId       => 3930080;
    public override string Executable       => "NuclearOptionServer.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7778;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
        => $"-logFile server.log -limitframerate 30{(string.IsNullOrWhiteSpace(s.CustomArgs) ? "" : $" {s.CustomArgs}")}";

    public override Task PreStartAsync(GameServer s)
    {
        string configPath = Path.Combine(s.InstallPath, "DedicatedServerConfig.json");
        string missionDir = Path.Combine(s.InstallPath, "Missions");
        Directory.CreateDirectory(missionDir);

        var config = new
        {
            ServerName       = string.IsNullOrWhiteSpace(s.ServerName) ? "Nuclear Option Server" : s.ServerName,
            Password         = "",
            Hidden           = false,
            MaxPlayers       = s.MaxPlayers > 0 ? s.MaxPlayers : 16,
            ModdedServer     = false,
            Port             = new { IsOverride = true, Value = s.ServerPort > 0 ? s.ServerPort : 7777 },
            QueryPort        = new { IsOverride = true, Value = s.QueryPort  > 0 ? s.QueryPort  : 7778 },
            MissionDirectory = missionDir,
            BanListPaths     = new[] { "ban_list.txt" },
            DisableErrorKick = false,
            ErrorKickImmuneListPaths = Array.Empty<string>(),
            NoPlayerStopTime = 30.0,
            PostMissionDelay = 30.0,
            RotationType     = 0,
            MissionRotation  = new object[]
            {
                new { Key = new { Group = "BuiltIn", Name = "Escalation"       }, MaxTime = 7200.0 },
                new { Key = new { Group = "BuiltIn", Name = "Terminal Control" }, MaxTime = 7200.0 },
                new { Key = new { Group = "BuiltIn", Name = "Confrontation"    }, MaxTime = 3600.0 },
                new { Key = new { Group = "BuiltIn", Name = "Domination"       }, MaxTime = 3600.0 },
            },
            VoteKick = new
            {
                Enabled               = true,
                PassRatio             = 0.6,
                MinVotes              = 3,
                AutoBanThreshold      = 3,
                VoteDuration          = 45.0,
                ResolutionDisplayTime = 20.0,
                NewVoteLockout        = 10.0,
                RequesterCooldown     = 300.0,
            },
        };

        WriteConfigIfMissing(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        return Task.CompletedTask;
    }

    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;

    public override Dictionary<string, string> GetDefaultSettings() => new();
    public override List<ConfigField> GetConfigFields() => BaseFields();
}
