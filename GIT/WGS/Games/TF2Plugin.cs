using WGS.Models;

namespace WGS.Games;

public class TF2Plugin : GamePluginBase
{
    public override string GameId          => "tf2";
    public override string GameName        => "Team Fortress 2";
    public override string Description     => "Valve's iconic team-based multiplayer FPS";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 232250;
    public override int    GameStoreAppId  => 440;
    public override string Executable      => "srcds.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 24;
    public override bool   RequiresSteamLogin => true;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "cp_dustbowl");
        return $"-game tf -console -usercon +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "cp_dustbowl",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "cp_dustbowl",
                    Options = ["cp_dustbowl", "pl_badwater", "ctf_2fort", "koth_harvest_final", "cp_steel"] },
        ]);
        return fields;
    }
}
