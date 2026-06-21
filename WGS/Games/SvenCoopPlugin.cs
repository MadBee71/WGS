using WGS.Models;

namespace WGS.Games;

public class SvenCoopPlugin : GoldSrcPluginBase
{
    public override string GameId           => "svencoop";
    public override string GameName         => "Sven Co-op";
    public override string Description      => "Long-running co-op mod for the original Half-Life — standalone Steam release";
    public override string Category         => "FPS";
    public override int    SteamAppId       => 276060; // standalone Sven Co-op dedicated server, not the shared GoldSrc appid (90)
    public override int    GameStoreAppId   => 225840;
    public override string Executable       => "SvenDS.exe";
    public override int    DefaultMaxPlayers => 16;
    protected override string GameDir       => "svencoop";

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "svencoop1");
        return $"-console -game svencoop +map {map} -port {s.ServerPort} +maxplayers {s.MaxPlayers} -norestart";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "svencoop1",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "map", Label = "Map", FieldType = ConfigFieldType.Text, DefaultValue = "svencoop1" });
        return fields;
    }
}
