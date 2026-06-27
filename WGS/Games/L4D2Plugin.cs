using System.IO;
using WGS.Models;

namespace WGS.Games;

public class L4D2Plugin : GamePluginBase
{
    public override string GameId           => "l4d2";
    public override string GameName         => "Left 4 Dead 2";
    public override string Description      => "Left 4 Dead 2 co-op zombie survival server";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 222860;
    public override int    GameStoreAppId   => 550;
    public override string Executable       => "srcds.exe";
    public override int    DefaultPort      => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 8;
    public override bool   HasRcon          => true;
    public override bool   SupportsSourceMod  => true;
    public override bool   RequiresSteamLogin => true;

    public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();

    public override Task PreStartAsync(GameServer s)
    {
        var cfg = Path.Combine(s.InstallPath, "left4dead2", "cfg", "server.cfg");
        WriteConfigIfMissing(cfg, SourceCfg(s));
        return Task.CompletedTask;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var map  = S(s, "map", "c1m1_hotel");
        var mode = S(s, "mode", "coop");
        return $"-game left4dead2 -console -usercon +map {map} {mode} -port {s.ServerPort} +maxplayers {s.MaxPlayers}";
    }

    private static string SourceCfg(GameServer s) =>
        $"""
        hostname "{s.ServerName}"
        sv_password "{s.ServerPassword}"
        rcon_password "{s.RconPassword}"
        """;

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"]  = "c1m1_hotel",
        ["mode"] = "coop",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map",  Label = "Map",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "c1m1_hotel",
                Options = ["c1m1_hotel","c2m1_highway","c3m1_plankcountry","c4m1_milltown_a","c5m1_waterfront"] },
            new() { Key = "mode", Label = "Mode", FieldType = ConfigFieldType.Dropdown, DefaultValue = "coop",
                Options = ["coop","versus","survival","scavenge","realism"] },
        ]);
        return fields;
    }
}
