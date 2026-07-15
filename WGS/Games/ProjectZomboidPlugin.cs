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
        var identity  = S(s, "identity", "servertest");
        // -Duser.home points to the parent of InstallPath so Zomboid saves go to <parent>\Zomboid\
        var userHome  = System.IO.Directory.GetParent(s.InstallPath)?.FullName ?? s.InstallPath;

        return
            $"\"-Djava.awt.headless=true\" \"-Dzomboid.steam=1\" \"-Dzomboid.znetlog=1\" \"-Duser.home={userHome}\" " +
            "\"-XX:+UseZGC\" \"-XX:-CreateCoredumpOnCrash\" \"-XX:-OmitStackTraceInFastThrow\" " +
            "-Xms4g -Xmx8g \"-Djava.library.path=natives/;natives/win64/;.\" \"-Dstatistic=0\" " +
            "-cp \"java/;java/projectzomboid.jar/\" " +
            "zombie.network.GameServer " +
            $"-port {s.ServerPort} -servername {identity}" +
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
