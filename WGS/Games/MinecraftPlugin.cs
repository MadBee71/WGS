using System.IO;
using WGS.Models;

namespace WGS.Games;

public class MinecraftPlugin : MinecraftPluginBase
{
    public override string GameId        => "minecraft";
    public override string GameName      => "Minecraft Java";
    public override string Description   => "Minecraft Java Edition dedicated server — true vanilla, or Paper for plugins/performance";
    public override string Category      => "Sandbox";
    public override int    SteamAppId    => 0;
    public override string Executable    => "java";
    public override int    DefaultPort   => 25565;
    public override int    DefaultQueryPort => 25565;
    public override int    DefaultMaxPlayers => 20;
    public override bool   HasRcon       => true;
    public override string MinecraftFlavor => "paper";

    // "Minecraft Java" defaults to true vanilla (Mojang's own server.jar) — a user picking the
    // game literally named that shouldn't silently get a different server implementation.
    // Paper is an explicit, visible opt-in for people who want plugin support/performance.
    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        var serverType = S(server, "serverType", "vanilla");
        var version = S(server, "mcVersion", "");
        if (string.IsNullOrWhiteSpace(version))
        {
            version = await MinecraftInstallHelper.GetLatestReleaseVersionAsync();
            if (version == null) { log("[Minecraft] Couldn't determine the latest Minecraft version."); return false; }
            log($"[Minecraft] No version specified — using latest release: {version}");
        }

        var url = serverType == "paper"
            ? await MinecraftInstallHelper.GetPaperJarUrlAsync(version)
            : await MinecraftInstallHelper.GetVanillaServerJarUrlAsync(version);

        if (url == null) { log($"[Minecraft] No {serverType} build found for Minecraft {version}."); return false; }

        var jar = S(server, "jarFile", "server.jar");
        await MinecraftInstallHelper.DownloadFileAsync(url, Path.Combine(server.InstallPath, jar), log);
        MinecraftInstallHelper.WriteEulaIfMissing(server.InstallPath);
        server.GameSpecificSettings["installedBuild"] = version;
        return true;
    }

    public override string BuildStartArguments(GameServer s)
    {
        var ram = S(s, "ramGb", "4");
        var jar = S(s, "jarFile", "server.jar");
        return $"-Xmx{ram}G -Xms{ram}G -jar \"{s.InstallPath}\\{jar}\" nogui";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["ramGb"]       = "4",
        ["jarFile"]     = "server.jar",
        ["serverType"]  = "vanilla",
        ["mcVersion"]   = "",
        ["difficulty"]  = "normal",
        ["gamemode"]    = "survival",
        ["onlineMode"]  = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverType", Label = "Server type", FieldType = ConfigFieldType.Dropdown, DefaultValue = "vanilla", Options = ["vanilla", "paper"],
                    Description = "Vanilla = Mojang's own server, exactly as released. Paper = same game, but supports plugins and runs noticeably better with more players." },
            new() { Key = "ramGb",      Label = "RAM (GB)",    FieldType = ConfigFieldType.Slider,   DefaultValue = "4",  Min = 1, Max = 32 },
            new() { Key = "jarFile",    Label = "Server JAR",  FieldType = ConfigFieldType.Text,     DefaultValue = "server.jar" },
            new() { Key = "mcVersion",  Label = "Minecraft version", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "e.g. 1.21.4. Leave empty to use the latest release on Install/Update." },
            new() { Key = "difficulty", Label = "Difficulty",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "normal", Options = ["peaceful","easy","normal","hard"] },
            new() { Key = "gamemode",   Label = "Game mode",   FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival", Options = ["survival","creative","adventure","spectator"] },
            new() { Key = "onlineMode", Label = "Online mode", FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
        ]);
        return fields;
    }
}
