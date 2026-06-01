using WGS.Models;

namespace WGS.Games;

public class EmpyrionPlugin : GamePluginBase
{
    public override string GameId           => "empyrion";
    public override string GameName         => "Empyrion - Galactic Survival";
    public override string Description      => "Space survival with building, exploration and multiplayer";
    public override string Category         => "Survival";
    public override int    SteamAppId       => 530870;
    public override int    GameStoreAppId   => 383120;
    public override string Executable       => "EmpyrionLauncher.exe";
    public override int    DefaultPort      => 30000;
    public override int    DefaultQueryPort => 30001;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
    {
        var saveName = S(s, "saveName", "MyGame");
        return $"-startDedicated -GameName \"{saveName}\" " +
               $"-ip 0.0.0.0 -port {s.ServerPort} -maxplayer {s.MaxPlayers}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["saveName"] = "MyGame",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "saveName", Label = "Save name", FieldType = ConfigFieldType.Text, DefaultValue = "MyGame" },
        ]);
        return fields;
    }
}
