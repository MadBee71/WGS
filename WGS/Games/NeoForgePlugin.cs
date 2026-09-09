using System.IO;
using System.Linq;
using WGS.Models;

namespace WGS.Games;

public class NeoForgePlugin : MinecraftPluginBase
{
    public override string GameId           => "minecraft_neoforge";
    public override string GameName         => "Minecraft NeoForge";
    public override string Description      => "Minecraft NeoForge modded server (Java Edition) — modern fork of Forge, used by most current FTB modpacks";
    public override string Category         => "Sandbox";
    public override int    SteamAppId       => 0;
    public override string Executable       => "java";
    public override int    DefaultPort      => 25565;
    public override int    DefaultQueryPort => 25565;
    public override int    DefaultMaxPlayers => 20;
    public override bool   HasRcon          => true;
    public override string MinecraftFlavor  => "neoforge";

    // NeoForge is a fork of Forge and uses the identical installer mechanism — same
    // --installServer flow, same win_args.txt/unix_args.txt output. See ForgePlugin.cs.
    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        var version = S(server, "mcVersion", "");
        if (string.IsNullOrWhiteSpace(version))
        {
            version = await MinecraftInstallHelper.GetLatestReleaseVersionAsync();
            if (version == null) { log("[Minecraft] Couldn't determine the latest Minecraft version."); return false; }
            log($"[Minecraft] No version specified — using latest release: {version}");
        }

        var url = await MinecraftInstallHelper.GetNeoForgeInstallerUrlAsync(version);
        if (url == null) { log($"[Minecraft] No NeoForge build found for Minecraft {version}."); return false; }

        var installerPath = Path.Combine(server.InstallPath, "neoforge-installer.jar");
        await MinecraftInstallHelper.DownloadFileAsync(url, installerPath, log);

        log("[Minecraft] Running NeoForge installer...");
        var javaExe = S(server, "javaPath", "");
        var ok = await MinecraftInstallHelper.RunJavaAsync(installerPath, "--installServer", server.InstallPath, log, timeoutMinutes: 10, javaExe: javaExe);
        if (!ok) { log("[Minecraft] NeoForge installer failed — check the output above for the actual error."); return false; }

        MinecraftInstallHelper.WriteEulaIfMissing(server.InstallPath);
        server.GameSpecificSettings["installedBuild"] = version;
        return true;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var ram = S(s, "ramGb", "4");

        if (Directory.Exists(s.InstallPath))
        {
            // NeoForge 1.20.1+: win_args.txt on Windows, unix_args.txt on Linux/Mac.
            // Without this file the required --add-opens/module flags are missing and NeoForge
            // crashes during native library initialisation (Netty kqueue, etc.) — same as Forge.
            var argsFiles = Directory.GetFiles(s.InstallPath, "win_args.txt", SearchOption.AllDirectories);
            if (argsFiles.Length == 0)
                argsFiles = Directory.GetFiles(s.InstallPath, "unix_args.txt", SearchOption.AllDirectories);
            if (argsFiles.Length > 0)
                return $"-Xmx{ram}G -Xms{ram}G @\"{argsFiles[0]}\" nogui";
        }

        // Unlike Forge, NeoForge has no legacy pre-args-file jar format to fall back to — every
        // NeoForge version needs win_args.txt/unix_args.txt. Reaching here means the install
        // hasn't completed (or failed silently), so this is a last-ditch attempt rather than a
        // real fallback: try any jar that isn't the installer, instead of guessing a filename
        // that the NeoForge installer never actually produces.
        var anyJar = Directory.Exists(s.InstallPath)
            ? Directory.GetFiles(s.InstallPath, "*.jar")
                .FirstOrDefault(f => !f.Contains("installer", StringComparison.OrdinalIgnoreCase))
            : null;
        return anyJar != null
            ? $"-Xmx{ram}G -Xms{ram}G -jar \"{anyJar}\" nogui"
            : $"-Xmx{ram}G -Xms{ram}G -jar \"{s.InstallPath}\\neoforge-server.jar\" nogui";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["ramGb"]      = "4",
        ["mcVersion"]  = "",
        ["difficulty"] = "normal",
        ["gamemode"]   = "survival",
        ["onlineMode"] = "true",
        ["javaPath"]   = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "ramGb",      Label = "RAM (GB)",   FieldType = ConfigFieldType.Slider,   DefaultValue = "4", Min = 2, Max = 64 },
            new() { Key = "mcVersion",  Label = "Minecraft version", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "e.g. 1.21.4. Leave empty to use the latest release. Required — NeoForge needs the exact Minecraft version to pick a matching build." },
            new() { Key = "difficulty", Label = "Difficulty", FieldType = ConfigFieldType.Dropdown, DefaultValue = "normal", Options = ["peaceful","easy","normal","hard"] },
            new() { Key = "gamemode",   Label = "Game mode",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival", Options = ["survival","creative","adventure","spectator"] },
            new() { Key = "onlineMode", Label = "Online mode",FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
            JavaPathField,
        ]);
        return fields;
    }
}
