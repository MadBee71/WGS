using System.IO;
using WGS.Models;

namespace WGS.Games;

public class DoDSPlugin : GamePluginBase
{
    public override string GameId           => "dods";
    public override string GameName         => "Day of Defeat: Source";
    public override string Description      => "Day of Defeat: Source WWII multiplayer server";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 232290;
    public override int    GameStoreAppId   => 300;
    public override string Executable       => "srcds.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 20;
    public override bool   HasRcon          => true;
    public override bool   SupportsSourceMod  => true;

    public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();

    public override Task PreStartAsync(GameServer s)
    {
        var cfg = Path.Combine(s.InstallPath, "dod", "cfg", "server.cfg");
        WriteConfigIfMissing(cfg, SourceCfg(s));
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "dod_anzio");
        return $"-game dod -console -usercon +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers}";
    }

    private static string SourceCfg(GameServer s) =>
        $"""
        hostname "{s.ServerName}"
        sv_password "{s.ServerPassword}"
        rcon_password "{s.RconPassword}"
        """;

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "dod_anzio",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "dod_anzio",
            Options = ["dod_anzio", "dod_avalanche", "dod_donner", "dod_flash", "dod_jagd", "dod_kalt", "dod_normandy"] });
        return fields;
    }
}
