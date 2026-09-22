using WGS.Models;

namespace WGS.Games;

public class ConanExilesPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId        => "conanexiles";
    public override string GameName      => "Conan Exiles";
    public override string Description   => "Open world survival in the Hyborian Age";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 443030;
    public override int    GameStoreAppId => 440900;
    public override int    WorkshopAppId  => 440900;

    public string ModTargetDirectory => @"ConanSandbox\Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable    => "ConanSandboxServer.exe";
    public override int    DefaultPort   => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 40;
    public override bool   HasRcon       => true;

    // Verified (22.9.2026): Conan Exiles' RCON has no save/quit/shutdown command — confirmed by
    // direct testing against real servers by third parties (several commonly-cited commands like
    // "save"/"quit" turned out not to exist at all). Stop always hard-kills; this is a genuine game
    // limitation, same category as Wreckfest/Valheim, not a missing WGS feature. RCON is still
    // usable for the generic "send RCON command" console (HasRcon above), just no dedicated
    // stop/kick/ban commands are wired here since their exact syntax isn't reliably documented.

    public string A2SHost => "127.0.0.1";
    public int GetA2SPort(Models.GameServer server) => server.QueryPort > 0 ? server.QueryPort : DefaultQueryPort;
    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "mapName", "game");
        return $"/Game/Maps/{map}/Map -log -noDNSLookup -MaxPlayers={s.MaxPlayers} -QueryPort={s.QueryPort} -Port={s.ServerPort}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mapName"] = "game",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "mapName", Label = "Kartta", FieldType = ConfigFieldType.Dropdown, DefaultValue = "game", Options = ["game","Siptah"] },
        ]);
        return fields;
    }
}
