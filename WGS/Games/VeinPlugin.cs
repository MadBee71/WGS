using WGS.Models;

namespace WGS.Games;

public class VeinPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId          => "vein";
    public override string GameName        => "Vein";
    public override string Description     => "Open world survival dedicated server";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2131400;
    public override int    WorkshopAppId   => 2131400;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override int    GameStoreAppId  => 2131400;
    public override string Executable      => @"Vein\Binaries\Win64\VeinServer-Win64-Test.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;
    public override bool   HasRcon         => true;

    // Verified (22.9.2026): no confirmed stdin/RCON stop command found for Vein's dedicated server
    // — not adding one rather than guessing. Separately: Vein has no admin-password concept at all
    // (confirmed via official wiki/host docs) — admin access is granted by SteamID allowlist
    // (AdminSteamIDs=/SuperAdminSteamIDs= in Game.ini), not a password, so the "adminPassword" field
    // below is dead/based on a wrong premise. Leaving it as-is for now — a real fix needs a SteamID
    // field + Game.ini writing, a bigger change than this pass's scope (missing stop commands).

    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} -multihome={s.ServerIp} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverPassword"] = "",
        ["adminPassword"]  = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverPassword", Label = "Server Password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
            new() { Key = "adminPassword",  Label = "Admin Password",  FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
