using WGS.Models;

namespace WGS.Games;

public class AmericanTruckSimulatorPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "ats";
    public override string GameName        => "American Truck Simulator";
    public override string Description     => "Convoy trucking server across the USA";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 2239530;
    public override int    WorkshopAppId   => 2239530;

    public string ModTargetDirectory => "mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModDownloadedAsync(s, w, id, n, "server_config.sii", "Mods");
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)
        => GroupDHelper.OnModRemovedAsync(s, id, "server_config.sii");
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override int    GameStoreAppId  => 270880;
    public override string Executable      => @"bin\win_x64\amtrucks_server.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
    {
        var cfg = S(s, "configFile", "server_config.sii");
        return $"-nosingle -server \"{s.InstallPath}\\{cfg}\"";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["configFile"] = "server_config.sii",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "configFile", Label = "Config-tiedosto", FieldType = ConfigFieldType.Text, DefaultValue = "server_config.sii" },
        ]);
        return fields;
    }
}
