using System.IO;
using WGS.Models;

namespace WGS.Games;

public class ForgePlugin : MinecraftPluginBase
{
    public override string GameId           => "minecraft_forge";
    public override string GameName         => "Minecraft Forge";
    public override string Description      => "Minecraft Forge modded server (Java Edition)";
    public override string Category         => "Sandbox";
    public override int    SteamAppId       => 0;
    public override string Executable       => "java";
    public override int    DefaultPort      => 25565;
    public override int    DefaultQueryPort => 25565;
    public override int    DefaultMaxPlayers => 20;
    public override bool   HasRcon          => true;
    public override string MinecraftFlavor  => "forge";

    public override string BuildStartArguments(GameServer s)
    {
        var ram = S(s, "ramGb", "4");

        if (Directory.Exists(s.InstallPath))
        {
            // Forge 1.17+: unix_args.txt JVM args file
            var argsFiles = Directory.GetFiles(s.InstallPath, "unix_args.txt", SearchOption.AllDirectories);
            if (argsFiles.Length > 0)
                return $"-Xmx{ram}G -Xms{ram}G @\"{argsFiles[0]}\" nogui";

            // Legacy Forge (<1.17): single forge-*.jar excluding installer
            var forgeJars = Directory.GetFiles(s.InstallPath, "*forge*.jar")
                .Where(f => !f.Contains("installer", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (forgeJars.Length > 0)
                return $"-Xmx{ram}G -Xms{ram}G -jar \"{forgeJars[0]}\" nogui";
        }

        return $"-Xmx{ram}G -Xms{ram}G -jar \"{s.InstallPath}\\forge-server.jar\" nogui";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["ramGb"]      = "4",
        ["difficulty"] = "normal",
        ["gamemode"]   = "survival",
        ["onlineMode"] = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "ramGb",      Label = "RAM (GB)",   FieldType = ConfigFieldType.Slider,   DefaultValue = "4", Min = 2, Max = 64 },
            new() { Key = "difficulty", Label = "Difficulty", FieldType = ConfigFieldType.Dropdown, DefaultValue = "normal", Options = ["peaceful","easy","normal","hard"] },
            new() { Key = "gamemode",   Label = "Game mode",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival", Options = ["survival","creative","adventure","spectator"] },
            new() { Key = "onlineMode", Label = "Online mode",FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
        ]);
        return fields;
    }
}
