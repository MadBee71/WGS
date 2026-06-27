using WGS.Models;

namespace WGS.Games;

public class BlackMesaPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "blackmesa";
    public override string GameName        => "Black Mesa";
    public override string Description     => "Source engine Black Mesa dedicated server";
    public override string Category        => "Shooter";
    public override int    SteamAppId      => 346680;
    public override int    GameStoreAppId  => 362890;
    public override int    WorkshopAppId   => 362890;

    public string ModTargetDirectory => @"bms/addons";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => "srcds.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 24;
    public override bool   HasRcon         => true;
    public override bool   SupportsSourceMod  => true;

        public override string  EngineFamily                                     => SourceRcon.Family;
    public override string? GetKickCommand(string p)                         => SourceRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => SourceRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => SourceRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => SourceRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => SourceRcon.Unban(p);
    public override string? GetPlayersCommand()                              => SourceRcon.Players();

    public override string BuildStartArguments(GameServer s)
    {
        var map     = S(s, "mapName", "gasworks");
        var tickrate = S(s, "tickrate", "64");
        return $"-game bms -console -map {map} -maxplayers {s.MaxPlayers} -port {s.ServerPort} -tickrate {tickrate}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mapName"]  = "gasworks",
        ["tickrate"] = "64",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "mapName",  Label = "Map",      FieldType = ConfigFieldType.Text,     DefaultValue = "gasworks" },
            new() { Key = "tickrate", Label = "Tickrate",  FieldType = ConfigFieldType.Dropdown, DefaultValue = "64", Options = ["64", "128"] },
        ]);
        return fields;
    }
}
