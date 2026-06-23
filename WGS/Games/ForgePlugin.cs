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

    // Forge has no plain server jar — you download their installer and run it with
    // --installServer, which writes either a unix_args.txt (1.17+) or a forge-*.jar (legacy)
    // into the install folder. BuildStartArguments below already auto-detects whichever shows up.
    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        var version = S(server, "mcVersion", "");
        if (string.IsNullOrWhiteSpace(version))
        {
            version = await MinecraftInstallHelper.GetLatestReleaseVersionAsync();
            if (version == null) { log("[Minecraft] Couldn't determine the latest Minecraft version."); return false; }
            log($"[Minecraft] No version specified — using latest release: {version}");
        }

        var url = await MinecraftInstallHelper.GetForgeInstallerUrlAsync(version);
        if (url == null) { log($"[Minecraft] No Forge build found for Minecraft {version}."); return false; }

        var installerPath = Path.Combine(server.InstallPath, "forge-installer.jar");
        await MinecraftInstallHelper.DownloadFileAsync(url, installerPath, log);

        log("[Minecraft] Running Forge installer...");
        var ok = await MinecraftInstallHelper.RunJavaAsync(installerPath, "--installServer", server.InstallPath, log, timeoutMinutes: 10);
        if (!ok) { log("[Minecraft] Forge installer failed — check the output above for the actual error."); return false; }

        MinecraftInstallHelper.WriteEulaIfMissing(server.InstallPath);
        server.GameSpecificSettings["installedBuild"] = version;
        return true;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var ram = S(s, "ramGb", "4");

        if (Directory.Exists(s.InstallPath))
        {
            // Forge 1.17+: win_args.txt on Windows, unix_args.txt on Linux/Mac.
            // Without this file the required --add-opens/module flags are missing and Forge
            // crashes during native library initialisation (Netty kqueue, etc.).
            var argsFiles = Directory.GetFiles(s.InstallPath, "win_args.txt", SearchOption.AllDirectories);
            if (argsFiles.Length == 0)
                argsFiles = Directory.GetFiles(s.InstallPath, "unix_args.txt", SearchOption.AllDirectories);
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
        ["mcVersion"]  = "",
        ["difficulty"] = "normal",
        ["gamemode"]   = "survival",
        ["onlineMode"] = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "ramGb",      Label = "RAM (GB)",   FieldType = ConfigFieldType.Slider,   DefaultValue = "4", Min = 2, Max = 64 },
            new() { Key = "mcVersion",  Label = "Minecraft version", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "e.g. 1.20.1. Leave empty to use the latest release's recommended Forge build." },
            new() { Key = "difficulty", Label = "Difficulty", FieldType = ConfigFieldType.Dropdown, DefaultValue = "normal", Options = ["peaceful","easy","normal","hard"] },
            new() { Key = "gamemode",   Label = "Game mode",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival", Options = ["survival","creative","adventure","spectator"] },
            new() { Key = "onlineMode", Label = "Online mode",FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
        ]);
        return fields;
    }
}
