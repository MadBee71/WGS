using WGS.Models;

namespace WGS.Games;

public class ARKSEPlugin : GamePluginBase, IWorkshopPlugin, IWipePlugin, IA2SQueryPlugin
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

    // ARK's stdin doesn't accept a stop command, so Stop always fell straight through to a hard
    // kill. "SaveWorld" then "DoExit" over RCON is the documented graceful sequence (confirmed
    // across multiple independent sources incl. arkids.net's command reference and long-standing
    // community consensus) — SaveWorld first because DoExit alone isn't guaranteed to save on
    // every platform/build.
    public override string[]? GetRconStopCommand(GameServer server) => ["SaveWorld", "DoExit"];


    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "mapName", "TheIsland");
        var args = $"{map}?listen?SessionName=\"{s.ServerName}\"?MultiHome={s.ServerIp}?Port={s.ServerPort}?MaxPlayers={s.MaxPlayers}?QueryPort={s.QueryPort}";
        if (!string.IsNullOrWhiteSpace(S(s, "serverPassword")))
            args += $"?ServerPassword={S(s, "serverPassword")}";
        // RCON only turns on when the user has actually set an RCON/admin password (ARK's
        // ServerAdminPassword doubles as the RCON password) — was entirely missing, so
        // GetRconStopCommand above could never actually connect and Stop silently fell through to
        // a hard kill despite looking correct in code (audit finding, 22.9.2026). Port defaults to
        // ServerPort+10 to match the fallback ServerManagerService.TrySendRconCommandAsync already
        // uses when RconPort isn't set, so the stop sequence connects to the port the server is
        // really listening on.
        if (!string.IsNullOrWhiteSpace(s.RconPassword))
        {
            var rconPort = s.RconPort > 0 ? s.RconPort : s.ServerPort + 10;
            args += $"?RCONEnabled=True?RCONPort={rconPort}?ServerAdminPassword={s.RconPassword}";
        }
        args += " -server -log";
        return args;
    }

    // IWipePlugin — ARK saves under ShooterGame/Saved/SavedArks/
    public IEnumerable<string> GetMapWipePaths(GameServer server)
    {
        var map = server.GameSpecificSettings.TryGetValue("mapName", out var m) ? m : "TheIsland";
        return [$@"ShooterGame\Saved\SavedArks\{map}.ark",
                $@"ShooterGame\Saved\SavedArks\{map}_AntiCorruptionBackup.bak",
                $@"ShooterGame\Saved\SavedArks\*.arktribe",
                $@"ShooterGame\Saved\SavedArks\*.arkprofile"];
    }

    public IEnumerable<string> GetFullWipePaths(GameServer server) => GetMapWipePaths(server);

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
