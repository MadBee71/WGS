using WGS.Models;

namespace WGS.Games;

public class FabricPlugin : GamePluginBase
{
    public override string GameId           => "minecraft_fabric";
    public override string GameName         => "Minecraft Fabric";
    public override string Description      => "Minecraft Fabric lightweight modded server";
    public override string Category         => "Sandbox";
    public override int    SteamAppId       => 0;
    public override string Executable       => "java";
    public override int    DefaultPort      => 25565;
    public override int    DefaultQueryPort => 25565;
    public override int    DefaultMaxPlayers => 20;
    public override bool   HasRcon          => true;
    public override string MinecraftFlavor  => "fabric";

    public override string  EngineFamily                                     => MinecraftRcon.Family;
    public override string? GetKickCommand(string p)                         => MinecraftRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => MinecraftRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => MinecraftRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => MinecraftRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => MinecraftRcon.Unban(p);
    public override string? GetPlayersCommand()                              => MinecraftRcon.Players();

    public override string BuildStartArguments(GameServer s)
    {
        var ram = S(s, "ramGb", "4");
        var jar = S(s, "jarFile", "fabric-server-launch.jar");
        return $"-Xmx{ram}G -Xms{ram}G -jar \"{s.InstallPath}\\{jar}\" nogui";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["ramGb"]      = "4",
        ["jarFile"]    = "fabric-server-launch.jar",
        ["difficulty"] = "normal",
        ["gamemode"]   = "survival",
        ["onlineMode"] = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "ramGb",      Label = "RAM (GB)",    FieldType = ConfigFieldType.Slider,   DefaultValue = "4", Min = 1, Max = 32 },
            new() { Key = "jarFile",    Label = "Fabric JAR",  FieldType = ConfigFieldType.Text,     DefaultValue = "fabric-server-launch.jar" },
            new() { Key = "difficulty", Label = "Difficulty",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "normal", Options = ["peaceful","easy","normal","hard"] },
            new() { Key = "gamemode",   Label = "Game mode",   FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival", Options = ["survival","creative","adventure","spectator"] },
            new() { Key = "onlineMode", Label = "Online mode", FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
        ]);
        return fields;
    }
}
