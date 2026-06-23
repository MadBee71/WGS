using System.IO;
using WGS.Models;

namespace WGS.Games;

public class FabricPlugin : MinecraftPluginBase
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

    // Unlike Forge, Fabric's meta API hands back a ready-to-run server jar directly —
    // no installer subprocess needed, just a download.
    public override async Task<bool> TryCustomInstallAsync(GameServer server, Action<string> log)
    {
        var version = S(server, "mcVersion", "");
        if (string.IsNullOrWhiteSpace(version))
        {
            version = await MinecraftInstallHelper.GetLatestReleaseVersionAsync();
            if (version == null) { log("[Minecraft] Couldn't determine the latest Minecraft version."); return false; }
            log($"[Minecraft] No version specified — using latest release: {version}");
        }

        var url = await MinecraftInstallHelper.GetFabricServerJarUrlAsync(version);
        if (url == null) { log($"[Minecraft] No Fabric loader found for Minecraft {version}."); return false; }

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
            new() { Key = "jarFile",    Label = "Fabric JAR", FieldType = ConfigFieldType.Text,     DefaultValue = "server.jar" },
            new() { Key = "mcVersion",  Label = "Minecraft version", FieldType = ConfigFieldType.Text, DefaultValue = "",
                    Description = "e.g. 1.21.4. Leave empty to use the latest release on Install/Update." },
            new() { Key = "difficulty", Label = "Difficulty", FieldType = ConfigFieldType.Dropdown, DefaultValue = "normal", Options = ["peaceful","easy","normal","hard"] },
            new() { Key = "gamemode",   Label = "Game mode",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "survival", Options = ["survival","creative","adventure","spectator"] },
            new() { Key = "onlineMode", Label = "Online mode",FieldType = ConfigFieldType.Toggle,   DefaultValue = "true" },
        ]);
        return fields;
    }
}
