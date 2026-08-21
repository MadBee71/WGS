using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WGS.Models;

namespace WGS.Games;

public class EcoGlobalSurvivalPlugin : GamePluginBase
{
    public override string GameId           => "ecoglobalsurvival";
    public override string GameName         => "Eco: Global Survival";
    public override string Description      => "Multiplayer survival game about building a civilization without destroying the ecosystem";
    public override string Category         => "Survival";
    public override int    SteamAppId       => 739590;
    public override int    GameStoreAppId   => 382310;
    public override string Executable       => "EcoServer.exe";
    public override int    DefaultPort      => 3000;
    public override int    DefaultQueryPort => 3001;
    public override int    DefaultMaxPlayers => 20;
    public override bool   HasRcon          => true;

    public override string BuildStartArguments(GameServer s)
    {
        var args = $"-userId=1 -port={s.ServerPort} -webPort={s.QueryPort}";
        var token = S(s, "ecoServerToken");
        if (!string.IsNullOrWhiteSpace(token))
            args += $" -userToken=\"{token}\"";
        return args;
    }

    public override async Task PreStartAsync(GameServer server)
    {
        var cfgPath = Path.Combine(server.InstallPath, "Configs", "Network.eco");
        if (!File.Exists(cfgPath)) return;

        JsonObject obj;
        try { obj = JsonNode.Parse(await File.ReadAllTextAsync(cfgPath))?.AsObject() ?? new(); }
        catch { return; }

        obj["Description"]     = server.ServerName;
        obj["GameServerPort"]  = server.ServerPort;
        obj["WebServerPort"]   = server.QueryPort;
        if (server.SteamPort > 0)
            obj["SteamServerPort"] = server.SteamPort;
        if (server.RconPort > 0)
            obj["RconServerPort"] = server.RconPort;
        if (!string.IsNullOrEmpty(server.RconPassword))
            obj["RconServerPassword"] = server.RconPassword;
        if (!string.IsNullOrEmpty(server.ServerPassword))
            obj["Password"] = server.ServerPassword;

        await File.WriteAllTextAsync(cfgPath,
            obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public override string? GetKickCommand(string playerName)
        => $"kick {playerName}";

    public override string? GetKickCommand(string playerName, string reason)
        => $"kick {playerName} {reason}";

    public override string? GetBanCommand(string playerName)
        => $"ban {playerName}";

    public override string? GetBanCommand(string playerName, string reason)
        => $"ban {playerName} {reason}";

    public override string? GetUnbanCommand(string playerName)
        => $"unban {playerName}";

    public override string? GetPlayersCommand()
        => "manage players";

    public override List<string> ConfigFiles => [
        "Configs/Network.eco",
        "Configs/Difficulty.eco",
        "Configs/Backup.eco",
        "Configs/Balance.eco",
        "Configs/EcoSim.eco",
        "Configs/WorldGenerator.eco",
        "Configs/Users.eco",
        "Configs/Performance.eco",
    ];

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new ConfigField
        {
            Key          = "ecoServerToken",
            Label        = "Server Token",
            Description  = "Optional token from play.eco to list your server publicly on the official server list",
            FieldType    = ConfigFieldType.Text,
            DefaultValue = "",
        });
        return fields;
    }
}
