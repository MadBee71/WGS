using WGS.Models;

namespace WGS.Games;

public class RisingStorm2VietnamPlugin : GamePluginBase
{
    public override string GameId          => "rs2vietnam";
    public override string GameName        => "Rising Storm 2: Vietnam";
    public override string Description     => "Large-scale Vietnam War tactical FPS on Unreal Engine 3";
    public override string Category        => "Military";
    public override int    SteamAppId      => 418480;
    public override int    GameStoreAppId  => 418460;
    public override string Executable      => @"Binaries\Win64\VNGame.exe";
    public override string? GetWorkingDirectory(GameServer server) => server.InstallPath;
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 64;

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "VNTE-SongBe");
        return $"{map}?MaxPlayers={s.MaxPlayers} -Port={s.ServerPort} -QueryPort={s.QueryPort} -Log=ServerLog.log";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "VNTE-SongBe",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Text, DefaultValue = "VNTE-SongBe" },
        ]);
        return fields;
    }
}
