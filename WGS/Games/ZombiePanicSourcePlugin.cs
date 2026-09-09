using WGS.Models;

namespace WGS.Games;

public class ZombiePanicSourcePlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "zps";
    public override string GameName        => "Zombie Panic! Source";
    public override string Description     => "Free Source engine co-op zombie survival mod";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 17505;
    public override int    GameStoreAppId  => 17500;
    public override string Executable      => "srcds.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 24;
    public override bool   RequiresSteamLogin => true;
    public override bool   HasRcon         => true;
    public override bool   SupportsSourceMod  => true;

    public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();

    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "zp_apartments_v3");
        return $"-game zps -console -usercon +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "zp_apartments_v3",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Text, DefaultValue = "zp_apartments_v3" },
        ]);
        return fields;
    }
}
