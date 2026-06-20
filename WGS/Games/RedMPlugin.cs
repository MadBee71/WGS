using System.IO;
using WGS.Models;

namespace WGS.Games;

public class RedMPlugin : GamePluginBase
{
    public override string GameId          => "redm";
    public override string GameName        => "Red Dead Redemption 2 (RedM)";
    public override string Description     => "Red Dead Redemption 2 multiplayer — FXServer is downloaded automatically";
    public override string Category        => "Open World";
    public override int    SteamAppId      => 0;   // Not on Steam; install manually from cfx.re/redm
    public override int    GameStoreAppId  => 1174180; // RDR2's own Steam app — used for the cover image only
    public override string Executable      => @"server\FXServer.exe";
    public override int    DefaultPort     => 30120;
    public override int    DefaultQueryPort => 30120;
    public override int    DefaultMaxPlayers => 32;
    public override bool   HasRcon          => true;
    public override bool   SupportsVersionCheck => true;

    public override async Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
    {
        var useLatest = S(server, "buildChannel", "recommended") == "latest";
        var info = useLatest ? await CfxArtifactHelper.GetLatestAsync() : await CfxArtifactHelper.GetRecommendedAsync();
        return info == null ? null : (info.Build, info.DownloadUrl);
    }

    public override async Task<string?> CheckForUpdateAsync(GameServer server)
    {
        var installed = S(server, "installedBuild", "");
        var info = await GetManualDownloadInfoAsync(server);
        return info != null && info.Value.Build != installed ? info.Value.Build : null;
    }

    public override async Task<(string? Recommended, string? Latest)> GetAvailableBuildsAsync(GameServer server)
    {
        var recommended = await CfxArtifactHelper.GetRecommendedAsync();
        var latest = await CfxArtifactHelper.GetLatestAsync();
        return (recommended?.Build, latest?.Build);
    }

    public override Task PreStartAsync(GameServer s)
    {
        var cfgPath = Path.Combine(s.InstallPath, "server", "server.cfg");
        WriteConfigIfMissing(cfgPath, BuildServerCfg(s));
        return Task.CompletedTask;
    }

    private static string BuildServerCfg(GameServer s)
    {
        var licenseKey = s.GameSpecificSettings.TryGetValue("licenseKey", out var lk) ? lk : "YOUR_LICENSE_KEY";

        return
            $"""
            # RedM runs the same FXServer binary as FiveM — this line is what actually switches it
            # into RedM/RDR3 mode instead of defaulting to GTA V/FiveM.
            set gamename rdr3

            sv_licenseKey "{licenseKey}"
            sv_maxclients {s.MaxPlayers}
            sv_hostname "{s.ServerName}"
            sv_projectName "{s.ServerName}"

            endpoint_add_tcp "0.0.0.0:{s.ServerPort}"
            endpoint_add_udp "0.0.0.0:{s.ServerPort}"

            set onesync on

            # Uncommented automatically by WGS — RCON requires a non-empty password to actually enable it.
            set rcon_password "{s.RconPassword}"

            # Default resources — without these, nothing loads at all (no map, no chat, no spawning).
            ensure mapmanager
            ensure chat
            ensure spawnmanager
            ensure sessionmanager
            ensure rconlog

            # Add your own resources below
            # ensure your-resource-name
            """;
    }

    public override string BuildStartArguments(GameServer s)
        => $"+set citizen_dir \"{s.InstallPath}\\server\\citizen\" +exec server.cfg";

    public override string? GetStopCommand(GameServer server) => "quit";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["licenseKey"]   = "",
        ["buildChannel"] = "recommended",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "licenseKey", Label = "CFX License Key", FieldType = ConfigFieldType.Password, DefaultValue = "",
                            Description = "Get your key from https://keymaster.fivem.net" });
        fields.Add(new() { Key = "buildChannel", Label = "FXServer build channel", FieldType = ConfigFieldType.Dropdown,
                            DefaultValue = "recommended", Options = ["recommended", "latest"],
                            Description = "Recommended = stable, what Cfx.re currently recommends. Latest = newest features, can be buggy." });
        return fields;
    }
}
