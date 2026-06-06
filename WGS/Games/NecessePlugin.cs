using WGS.Models;

namespace WGS.Games;

public class NecessePlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "necesse";
    public override string GameName        => "Necesse";
    public override string Description     => "Top-down medieval survival and settlement building";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1169370;
    public override int    GameStoreAppId  => 1169370;
    public override int    WorkshopAppId   => 1169370;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    // Necesse bundles its own JRE; the server is launched via java.exe -jar Server.jar
    public override string Executable      => @"jre\bin\java.exe";
    public override int    DefaultPort     => 14159;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 32;

    public override string BuildStartArguments(GameServer s)
    {
        var world = S(s, "worldName", "MyWorld");
        return $"-jar Server.jar -world \"{world}\" -port {s.ServerPort} -slots {s.MaxPlayers}";
    }

    public override string? GetStopCommand(GameServer server) => "stop";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["worldName"] = "MyWorld",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName", Label = "Maailman nimi", FieldType = ConfigFieldType.Text, DefaultValue = "MyWorld" },
        ]);
        return fields;
    }
}
