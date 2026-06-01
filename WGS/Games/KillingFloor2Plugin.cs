using WGS.Models;

namespace WGS.Games;

public class KillingFloor2Plugin : GamePluginBase
{
    public override string GameId          => "kf2";
    public override string GameName        => "Killing Floor 2";
    public override string Description     => "Co-op survival horror shooter against waves of Zeds";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 232130;
    public override int    GameStoreAppId  => 232090;
    public override string Executable      => @"Binaries\Win64\KFServer.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultSteamPort => 20560;
    public override int    DefaultMaxPlayers => 6;
    public override bool   RequiresSteamLogin => true;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s)
    {
        var map  = S(s, "map",        "KF-BioticsLab");
        var diff = S(s, "difficulty", "2");
        var len  = S(s, "gameLength", "1");
        return $"{map}?game=KFGameContent.KFGameInfo_Survival?difficulty={diff}?GameLength={len} -Port={s.ServerPort} -QueryPort={s.QueryPort}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"]        = "KF-BioticsLab",
        ["difficulty"] = "2",
        ["gameLength"] = "1",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map",        Label = "Map",         FieldType = ConfigFieldType.Text,     DefaultValue = "KF-BioticsLab" },
            new() { Key = "difficulty", Label = "Difficulty",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "2",
                    Options = ["0 - Normal", "1 - Hard", "2 - Suicidal", "3 - Hell on Earth"] },
            new() { Key = "gameLength", Label = "Game Length", FieldType = ConfigFieldType.Dropdown, DefaultValue = "1",
                    Options = ["0 - Short", "1 - Medium", "2 - Long"] },
        ]);
        return fields;
    }
}
