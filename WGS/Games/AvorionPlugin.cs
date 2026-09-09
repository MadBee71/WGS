using WGS.Models;

namespace WGS.Games;

public class AvorionPlugin : GamePluginBase
{
    public override string GameId          => "avorion";
    public override string GameName        => "Avorion";
    public override string Description     => "Space sandbox building game — build, mine and fight across a procedural galaxy";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 565060;
    public override int    GameStoreAppId  => 445220;
    public override string Executable      => @"bin\AvorionServer.exe";
    public override string? GetWorkingDirectory(GameServer server) => server.InstallPath;
    public override int    DefaultPort     => 27000;
    public override int    DefaultQueryPort => 27000;
    public override int    DefaultMaxPlayers => 10;

    public override string BuildStartArguments(GameServer s)
    {
        var galaxy = S(s, "galaxyName", "avorion_galaxy");
        var name   = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Avorion Server" : s.ServerName;
        var admin  = S(s, "adminSteamId", "");
        var adminArg = string.IsNullOrWhiteSpace(admin) ? "" : $"--admin {admin}";
        return $"--galaxy-name \"{galaxy}\" --port {s.ServerPort} --max-players {s.MaxPlayers} --server-name \"{name}\" {adminArg}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["galaxyName"]   = "avorion_galaxy",
        ["adminSteamId"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "galaxyName", Label = "Galaxy name", FieldType = ConfigFieldType.Text, DefaultValue = "avorion_galaxy" },
            new() { Key = "adminSteamId", Label = "Admin Steam ID(s)", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "SteamID64 of the server admin. Space-separate multiple IDs." },
        ]);
        return fields;
    }
}
