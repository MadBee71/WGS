using WGS.Models;

namespace WGS.Games;

public class MinecraftPlugin : GamePluginBase
{
    public override string GameId        => "minecraft";
    public override string GameName      => "Minecraft Java";
    public override string Description   => "Minecraft Java Edition dedicated server";
    public override string Category      => "Sandbox";
    public override int    SteamAppId    => 0; // not on Steam
    public override string Executable    => "java";
    public override int    DefaultPort   => 25565;
    public override int    DefaultQueryPort => 25565;
    public override int    DefaultMaxPlayers => 20;
    public override bool   HasRcon         => true;
    public override string MinecraftFlavor => "paper";

    public override string BuildStartArguments(GameServer s)
    {
        var ram  = S(s, "ramGb", "4");
        var jar  = S(s, "jarFile", "server.jar");
        return $"-Xmx{ram}G -Xms{ram}G -jar \"{s.InstallPath}\\{jar}\" nogui";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["ramGb"]       = "4",
        ["jarFile"]     = "server.jar",
        ["difficulty"]  = "normal",
        ["gamemode"]    = "survival",
        ["onlineMode"]  = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "ramGb",      Label = "RAM (GB)",        FieldType = ConfigFieldType.Slider,   DefaultValue = "4",  Min = 1, Max = 32 },
            new() { Key = "jarFile",    Label = "Server JAR",      FieldType = ConfigFieldType.Text,     DefaultValue = "server.jar" },
            new() { Key = "difficulty", Label = "Vaikeustaso",     FieldType = ConfigFieldType.Dropdown, DefaultValue = "normal", Options = ["peaceful","easy","normal","hard"] },
            new() { Key = "gamemode",   Label = "Pelitila",        FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival", Options = ["survival","creative","adventure","spectator"] },
            new() { Key = "onlineMode", Label = "Online-tila",     FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
        ]);
        return fields;
    }
}
