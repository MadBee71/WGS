using WGS.Models;

namespace WGS.Games;

public class ProjectZomboidPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId          => "projectzomboid";
    public override string GameName        => "Project Zomboid";
    public override string Description     => "Hardcore zombie survival RPG with deep simulation";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 380870;
    public override int    GameStoreAppId  => 108600;
    public override int    WorkshopAppId   => 108600;

    public string ModTargetDirectory => "mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;

    // Project Zomboid has no standalone exe — launched via its bundled JRE.
    // Working directory must be InstallPath (not jre64\bin\) so Windows finds SDL3.dll and other natives.
    public override string Executable      => @"jre64\bin\java.exe";
    public override string? GetWorkingDirectory(GameServer server) => server.InstallPath;
    public override int    DefaultPort     => 16261;
    public override int    DefaultQueryPort => 16262;
    public override int    DefaultMaxPlayers => 32;
    public override bool   HasRcon         => true;

    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;

    public override string BuildStartArguments(GameServer s)
    {
        var identity = S(s, "identity", "servertest");
        var userHome = System.IO.Directory.GetParent(s.InstallPath)?.FullName ?? s.InstallPath;

        // Build absolute classpath from install root so working directory doesn't matter
        var javaDir   = System.IO.Path.Combine(s.InstallPath, "java");
        var b41Jars   = new[]
        {
            "istack-commons-runtime.jar", "jassimp.jar", "javacord-2.0.17-shaded.jar",
            "javax.activation-api.jar", "jaxb-api.jar", "jaxb-runtime.jar", "lwjgl.jar",
            "lwjgl-natives-windows.jar", "lwjgl-glfw.jar", "lwjgl-glfw-natives-windows.jar",
            "lwjgl-jemalloc.jar", "lwjgl-jemalloc-natives-windows.jar", "lwjgl-opengl.jar",
            "lwjgl-opengl-natives-windows.jar", "lwjgl_util.jar", "sqlite-jdbc-3.27.2.1.jar",
            "trove-3.0.3.jar", "uncommons-maths-1.2.3.jar", "commons-compress-1.18.jar",
        };
        // Use all jars found in java/ — covers both B41 and B42
        string classpath;
        if (System.IO.Directory.Exists(javaDir))
        {
            var jars = System.IO.Directory.GetFiles(javaDir, "*.jar")
                           .Select(j => $"\"{j}\"");
            classpath = string.Join(";", jars) + $";\"{javaDir}\"";
        }
        else
        {
            classpath = string.Join(";", b41Jars.Select(j => $"\"{System.IO.Path.Combine(javaDir, j)}\""))
                        + $";\"{javaDir}\"";
        }

        var natives = $"{s.InstallPath}\\natives;{s.InstallPath}\\natives\\win64;{s.InstallPath}";
        return
            $"-Djava.awt.headless=true -Dzomboid.steam=1 -Dzomboid.znetlog=1 " +
            $"-XX:+UseZGC -XX:-CreateCoredumpOnCrash -XX:-OmitStackTraceInFastThrow " +
            $"-Xms4g -Xmx8g \"-Djava.library.path={natives}\" " +
            $"-cp {classpath} zombie.network.GameServer " +
            $"-statistic 0 -port {s.ServerPort} -servername {identity}" +
            (string.IsNullOrWhiteSpace(s.CustomArgs) ? "" : $" {s.CustomArgs}");
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["identity"] = "servertest",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "identity", Label = "Server profile name", FieldType = ConfigFieldType.Text, DefaultValue = "servertest" },
        ]);
        return fields;
    }
}
