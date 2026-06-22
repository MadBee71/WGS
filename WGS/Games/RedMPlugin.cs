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

    // No server.cfg, no resources, no txData pre-seeding — FXServer's own txAdmin sets all of
    // that up itself on first run (setup wizard, recipe choice, resource downloads). WGS's job
    // here is just getting FXServer.exe downloaded and started.
    public override Task PreStartAsync(GameServer s) => Task.CompletedTask;

    // citizen_dir is a launch arg FXServer itself requires to find its RDR3 runtime files —
    // unrelated to server.cfg/resources, so it stays even without our own config generation.
    public override string BuildStartArguments(GameServer s)
        => $"+set citizen_dir \"{s.InstallPath}\\server\\citizen\"";

    public override string? GetStopCommand(GameServer server) => "quit";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["buildChannel"] = "recommended",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "buildChannel", Label = "FXServer build channel", FieldType = ConfigFieldType.Dropdown,
                            DefaultValue = "recommended", Options = ["recommended", "latest"],
                            Description = "Recommended = stable, what Cfx.re currently recommends. Latest = newest features, can be buggy. The CFX license key, RCON password and everything else are set inside txAdmin itself after first launch — not here." });
        return fields;
    }
}
