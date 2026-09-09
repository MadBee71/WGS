using WGS.Models;

namespace WGS.Games;

public class CSSPlugin : GamePluginBase, IA2SQueryPlugin
{
    public override string GameId          => "css";
    public override string GameName        => "Counter-Strike: Source";
    public override string Description     => "Valve's classic tactical FPS on the Source engine";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 232330;
    public override int    GameStoreAppId  => 240;
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
        var map = S(s, "map", "de_dust2");
        return $"-game cstrike -console -usercon +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "de_dust2",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "de_dust2",
                    Options = ["de_dust2", "de_dust", "de_inferno", "de_nuke", "de_train", "cs_office", "cs_italy"] },
        ]);
        return fields;
    }
}
