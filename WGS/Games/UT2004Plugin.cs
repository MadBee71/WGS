using WGS.Models;

namespace WGS.Games;

public class UT2004Plugin : GamePluginBase
{
    public override string GameId          => "ut2004";
    public override string GameName        => "Unreal Tournament 2004";
    public override string Description     => "Classic arena FPS — legally free since Epic Games authorized Internet Archive distribution";
    public override string Category        => "FPS";
    public override int    SteamAppId      => 0;
    public override string Executable      => @"System\UCC.exe";
    public override string? GetWorkingDirectory(GameServer server) => server.InstallPath;
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 7778;
    public override int    DefaultMaxPlayers => 16;

    // Verified working: Epic Games authorized the Internet Archive to host UT2004 freely
    // (the same policy applied to Unreal Gold and the original Unreal Tournament) — this is the
    // official dedicated server package including the Bonus Pack, confirmed live (HTTP 200/302,
    // real ~725MB payload) as of this writing.
    public override Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
        => Task.FromResult<(string, string)?>(("3369.3", "https://archive.org/download/ut2004-server/dedicatedserver3369.3-bonuspack.zip"));

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "CTF-Face3");
        var gametype = S(s, "gameType", "XGame.xCTFGame");
        return $"server {map}?Game={gametype} -port={s.ServerPort} ini=UT2004.ini log=server.log";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"]      = "CTF-Face3",
        ["gameType"] = "XGame.xCTFGame",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Text, DefaultValue = "CTF-Face3" },
            new() { Key = "gameType", Label = "Game type", FieldType = ConfigFieldType.Dropdown, DefaultValue = "XGame.xCTFGame",
                    Options = ["XGame.xDeathMatch", "XGame.xTeamGame", "XGame.xCTFGame", "ONS.ONSOnslaughtGame"] },
        ]);
        return fields;
    }
}
