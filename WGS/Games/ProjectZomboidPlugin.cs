using WGS.Models;

namespace WGS.Games;

public class ProjectZomboidPlugin : GamePluginBase, IWorkshopPlugin
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

    // Project Zomboid has no standalone exe — launched via its bundled JRE
    public override string Executable      => @"jre64\bin\java.exe";
    public override int    DefaultPort     => 16261;
    public override int    DefaultQueryPort => 16262;
    public override int    DefaultMaxPlayers => 32;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s)
    {
        var identity = S(s, "identity", "servertest");
        var userHome = System.IO.Directory.GetParent(s.InstallPath)?.FullName ?? s.InstallPath;

        // Read classpath from StartServer64.bat if present, otherwise use known B41 default
        var batPath   = System.IO.Path.Combine(s.InstallPath, "StartServer64.bat");
        var classpath = "java/istack-commons-runtime.jar;java/jassimp.jar;java/javacord-2.0.17-shaded.jar;" +
                        "java/javax.activation-api.jar;java/jaxb-api.jar;java/jaxb-runtime.jar;java/lwjgl.jar;" +
                        "java/lwjgl-natives-windows.jar;java/lwjgl-glfw.jar;java/lwjgl-glfw-natives-windows.jar;" +
                        "java/lwjgl-jemalloc.jar;java/lwjgl-jemalloc-natives-windows.jar;java/lwjgl-opengl.jar;" +
                        "java/lwjgl-opengl-natives-windows.jar;java/lwjgl_util.jar;java/sqlite-jdbc-3.27.2.1.jar;" +
                        "java/trove-3.0.3.jar;java/uncommons-maths-1.2.3.jar;java/commons-compress-1.18.jar;java/";
        if (System.IO.File.Exists(batPath))
        {
            var line = System.IO.File.ReadLines(batPath)
                           .FirstOrDefault(l => l.TrimStart().StartsWith("SET PZ_CLASSPATH="));
            if (line != null)
                classpath = line.Substring(line.IndexOf('=') + 1).Trim();
        }

        return
            $"-Djava.awt.headless=true -Dzomboid.steam=1 -Dzomboid.znetlog=1 " +
            $"-XX:+UseZGC -XX:-CreateCoredumpOnCrash -XX:-OmitStackTraceInFastThrow " +
            $"-Xms4g -Xmx8g -Djava.library.path=natives/;natives/win64/;. " +
            $"-cp \"{classpath}\" zombie.network.GameServer " +
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
