using WGS.Models;

namespace WGS.Games;

public class ValheimPlugin : GamePluginBase, IWorkshopPlugin, IA2SQueryPlugin
{
    public override string GameId        => "valheim";
    public override string GameName      => "Valheim";
    public override string Description   => "Viking survival co-op up to 10 players";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 896660;
    public override int    GameStoreAppId => 892970;
    public override int    WorkshopAppId  => 896660;

    public string ModTargetDirectory => "BepInEx/plugins";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable    => "valheim_server.exe";
    public override int    DefaultPort   => 2456;
    public override int    DefaultQueryPort => 2457;
    public override int    DefaultMaxPlayers => 10;
    protected override bool FilterUnityShaderNoise => true;

    public string A2SHost => "127.0.0.1";

    // Valheim has no -queryport (or any) command-line flag to control this — confirmed against
    // the Valheim Wiki's dedicated server docs, and by BuildStartArguments above never passing
    // one. The server always answers A2S on ServerPort+1, full stop. Querying the user-editable
    // "Query Port" setting instead only works when that field happens to equal ServerPort+1 — a
    // real community bug (SkOODaT, Discord, 12.9.2026): a server with a mismatched Query Port
    // silently always reported 0 players, which fed straight into "shut down when empty".
    public int GetA2SPort(Models.GameServer server) => server.ServerPort + 1;

    public override string? ValidateBeforeStart(GameServer server)
        => string.IsNullOrEmpty(server.ServerPassword) || server.ServerPassword.Length < 5
            ? "Valheim requires a server password of at least 5 characters, or the server will reject all connections. Set one in Settings → Password."
            : null;

    public override string BuildStartArguments(GameServer s)
    {
        // Server name comes from the dedicated, always-visible "Server name" field (s.ServerName)
        // like every other plugin uses — not a GameSpecificSettings key, which the Settings UI
        // filters out anyway since that base field already covers it.
        var name   = string.IsNullOrWhiteSpace(s.ServerName) ? "MyValheim" : s.ServerName;
        var world  = S(s, "worldName", "Dedicated");
        var pass   = s.ServerPassword;
        var cross  = S(s, "crossplay", "false") == "true" ? "-crossplay" : "";
        var pub    = S(s, "public", "true") == "true" ? "1" : "0";
        // Guard against a malformed/empty stored value breaking the launch command (matches the
        // TryParse-with-fallback pattern used for other numeric GameSpecificSettings elsewhere,
        // e.g. PalworldPlugin.RestApiPort) — ConfigFieldType.Number is a UI hint, not a guarantee.
        var saveInterval = int.TryParse(S(s, "saveInterval", "1800"), out var si) && si > 0 ? si : 1800;
        return $"-nographics -batchmode -name \"{name}\" -world \"{world}\" -password \"{pass}\" -port {s.ServerPort} -savedir \"{s.InstallPath}\\saves\" -public {pub} {cross} -saveinterval {saveInterval}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["worldName"]     = "Dedicated",
        ["crossplay"]     = "false",
        ["public"]        = "true",
        ["saveInterval"]  = "1800",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName",  Label = "World name",  FieldType = ConfigFieldType.Text,   DefaultValue = "Dedicated" },
            new() { Key = "crossplay",  Label = "Crossplay",      FieldType = ConfigFieldType.Toggle, DefaultValue = "false",
                    Description = "Routes the server through PlayFab instead of Steam — this reliably breaks WGS's player count/query (confirmed by community testing, Discord, 16-18.9.2026). No fix available: PlayFab's lobby API requires an auth flow WGS has no legitimate way to perform. Also note: PlayFab caches a server's address on its end for up to 2 hours after crossplay was enabled — turning it back off doesn't take effect for players immediately, they may need to wait it out or disconnect/reconnect PlayFab locally to force a refresh (SkOODaT, Discord, 18.9.2026)." },
            new() { Key = "public",     Label = "Public listing",  FieldType = ConfigFieldType.Toggle, DefaultValue = "true" },
            new() { Key = "saveInterval", Label = "Save interval (seconds)", FieldType = ConfigFieldType.Number, DefaultValue = "1800",
                    Description = "Valheim has no graceful-stop/force-save command WGS can send — Stop always ends in a hard kill, and any progress since the last autosave is lost. Defaults to Valheim's own 1800s (30 min) so WGS doesn't change existing server behavior. Lowering it shrinks the worst-case loss but triggers more frequent brief lag spikes when saving — confirmed by community testing (SkOODaT, Discord, 18.9.2026) that a short interval like 5 min is too laggy on an active world. Tune to taste." },
        ]);
        return fields;
    }
}
