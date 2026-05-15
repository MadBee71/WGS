using WGS.Models;

namespace WGS.Games;

public class CS2Plugin : GamePluginBase
{
    public override string GameId          => "cs2";
    public override string GameName        => "Counter-Strike 2";
    public override string Description     => "Valve's tactical FPS — CS2 dedicated server";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 730;
    public override int    GameStoreAppId  => 730;
    public override string Executable      => @"game\bin\win64\cs2.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 10;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s)
    {
        var map     = S(s, "map",      "de_dust2");
        var gameType = S(s, "gameType", "0");
        var gameMode = S(s, "gameMode", "0");
        return $"-dedicated -console -usercon -port {s.ServerPort} " +
               $"+map {map} +game_type {gameType} +game_mode {gameMode} " +
               $"+sv_setsteamaccount {s.Gslt} +maxplayers {s.MaxPlayers}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"]      = "de_dust2",
        ["gameType"] = "0",
        ["gameMode"] = "0",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map",      Label = "Oletuskartta",  FieldType = ConfigFieldType.Text,     DefaultValue = "de_dust2" },
            new() { Key = "gameType", Label = "Game type",     FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",
                    Options = ["0","1","2","3"] },
            new() { Key = "gameMode", Label = "Game mode",     FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",
                    Options = ["0","1","2"] },
        ]);
        return fields;
    }
}
