using WGS.Models;

namespace WGS.Games;

public class ARKSEPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "arkse";
    public override string GameName        => "ARK: Survival Evolved";
    public override string Description     => "Open world dinosaur survival";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 376030;
    public override int    GameStoreAppId  => 346110;
    public override int    WorkshopAppId   => 346110;

    public string ModTargetDirectory => @"ShooterGame/Content/Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"ShooterGame\Binaries\Win64\ShooterGameServer.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 70;
    public override bool   HasRcon         => true;

        public override string  EngineFamily                                     => ArkRcon.Family;
    public override string? GetKickCommand(string p)                         => ArkRcon.Kick(p);
    public override string? GetKickCommand(string p, string reason)          => ArkRcon.Kick(p, reason);
    public override string? GetBanCommand(string p)                          => ArkRcon.Ban(p);
    public override string? GetBanCommand(string p, string reason)           => ArkRcon.Ban(p, reason);
    public override string? GetUnbanCommand(string p)                        => ArkRcon.Unban(p);
    public override string? GetPlayersCommand()                              => ArkRcon.Players();

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "mapName", "TheIsland");
        var args = $"{map}?listen?SessionName=\"{s.ServerName}\"?MultiHome={s.ServerIp}?Port={s.ServerPort}?MaxPlayers={s.MaxPlayers}?QueryPort={s.QueryPort}";
        if (!string.IsNullOrWhiteSpace(S(s, "serverPassword")))
            args += $"?ServerPassword={S(s, "serverPassword")}";
        args += " -server -log";
        return args;
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mapName"]        = "TheIsland",
        ["serverPassword"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "mapName", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "TheIsland",
                Options = ["TheIsland", "TheCenter", "ScorchedEarth_P", "Ragnarok", "Aberration_P", "Extinction", "Genesis", "CrystalIsles", "Gen2"] },
            new() { Key = "serverPassword", Label = "Server Password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
