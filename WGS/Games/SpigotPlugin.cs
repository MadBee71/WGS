using System.IO;
using WGS.Models;

namespace WGS.Games;

public class SpigotPlugin : MinecraftPluginBase
{
    public override string GameId           => "minecraft_spigot";
    public override string GameName         => "Minecraft Spigot";
    public override string Description      => "Minecraft Spigot high-performance server with plugin support";
    public override string Category         => "Sandbox";
    public override int    SteamAppId       => 0;
    public override string Executable       => "java";
    public override int    DefaultPort      => 25565;
    public override int    DefaultQueryPort => 25565;
    public override int    DefaultMaxPlayers => 20;
    public override bool   HasRcon          => true;
    public override bool   HasHeavyInstall  => true;
    public override string MinecraftFlavor  => "spigot";

    private const string BuildToolsUrl = "https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar";

    // Spigot has no prebuilt download anywhere — official policy is everyone compiles their own
    // jar locally from source via BuildTools, which downloads Mojang's mappings and patches them
    // with CraftBukkit/Spigot's own changes. This is slow (minutes) and needs a JDK, unlike the
    // other three Minecraft variants which are just a file download.
    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        var version = S(server, "mcVersion", "");

        var buildToolsPath = Path.Combine(server.InstallPath, "BuildTools.jar");
        await MinecraftInstallHelper.DownloadFileAsync(BuildToolsUrl, buildToolsPath, log);

        log(string.IsNullOrWhiteSpace(version)
            ? "[Minecraft] Building latest Spigot from source — this compiles locally and can take several minutes..."
            : $"[Minecraft] Building Spigot {version} from source — this compiles locally and can take several minutes...");

        var args = string.IsNullOrWhiteSpace(version) ? "--output-dir ." : $"--rev {version} --output-dir .";
        var ok = await MinecraftInstallHelper.RunJavaAsync(buildToolsPath, args, server.InstallPath, log, timeoutMinutes: 20);
        if (!ok) { log("[Minecraft] BuildTools failed — check the output above for the actual error."); return false; }

        var newest = MinecraftInstallHelper.FindNewestFile(server.InstallPath, "spigot-*.jar");
        if (newest == null) { log("[Minecraft] BuildTools finished but no spigot-*.jar was found."); return false; }

        var targetJar = Path.Combine(server.InstallPath, S(server, "jarFile", "server.jar"));
        File.Copy(newest, targetJar, overwrite: true);
        MinecraftInstallHelper.WriteEulaIfMissing(server.InstallPath);

        log("[Minecraft] Cleaning up BuildTools artifacts...");
        CleanBuildToolsArtifacts(server.InstallPath);
        log("[Minecraft] Cleanup done.");
        return true;
    }

    private static void CleanBuildToolsArtifacts(string installPath)
    {
        foreach (var dir in new[] { "apache-maven-3.9.6", "BuildData", "Bukkit", "CraftBukkit", "Spigot", "work" })
        {
            var path = Path.Combine(installPath, dir);
            if (Directory.Exists(path)) try { Directory.Delete(path, recursive: true); } catch { }
        }
        foreach (var dir in Directory.GetDirectories(installPath, "PortableGit-*"))
            try { Directory.Delete(dir, recursive: true); } catch { }
        foreach (var file in new[] { "BuildTools.jar", "BuildTools.log.txt" })
        {
            var path = Path.Combine(installPath, file);
            if (File.Exists(path)) try { File.Delete(path); } catch { }
        }
    }

    public override Task PreStartAsync(GameServer s)
    {
        CleanBuildToolsArtifacts(s.InstallPath);
        return base.PreStartAsync(s);
    }

    public override string BuildStartArguments(GameServer s)
    {
        var ram = S(s, "ramGb", "4");
        var jar = S(s, "jarFile", "server.jar");
        return $"-Xmx{ram}G -Xms{ram}G -jar \"{s.InstallPath}\\{jar}\" nogui";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["ramGb"]      = "4",
        ["jarFile"]    = "server.jar",
        ["mcVersion"]  = "",
        ["difficulty"] = "normal",
        ["gamemode"]   = "survival",
        ["onlineMode"] = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "ramGb",      Label = "RAM (GB)",   FieldType = ConfigFieldType.Slider,   DefaultValue = "4", Min = 1, Max = 32 },
            new() { Key = "jarFile",    Label = "Spigot JAR", FieldType = ConfigFieldType.Text,     DefaultValue = "server.jar" },
            new() { Key = "mcVersion",  Label = "Minecraft version", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "e.g. 1.21.4. Leave empty for BuildTools' own latest. Compiling takes several minutes — this isn't a simple download." },
            new() { Key = "difficulty", Label = "Difficulty", FieldType = ConfigFieldType.Dropdown, DefaultValue = "normal", Options = ["peaceful","easy","normal","hard"] },
            new() { Key = "gamemode",   Label = "Game mode",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival", Options = ["survival","creative","adventure","spectator"] },
            new() { Key = "onlineMode", Label = "Online mode",FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
        ]);
        return fields;
    }
}
