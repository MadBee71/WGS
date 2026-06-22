using WGS.Models;

namespace WGS.Games;

public class FiveMPlugin : GamePluginBase
{
    public override string GameId           => "fivem";
    public override string GameName         => "Grand Theft Auto V (FiveM)";
    public override string Description      => "FiveM multiplayer modification framework for GTA V — FXServer is downloaded automatically";
    public override string Category         => "Open World";
    public override int    SteamAppId       => 0;
    public override int    GameStoreAppId   => 271590; // GTA V's own Steam app — used for the cover image only
    public override string Executable       => "FXServer.exe";
    public override int    DefaultPort      => 30120;
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

    public override string BuildStartArguments(GameServer s) => "";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["licenseKey"]   = "",
        ["buildChannel"] = "recommended",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "licenseKey",  Label = "CFX License Key",  FieldType = ConfigFieldType.Password, DefaultValue = "",
                    Description = "Not needed to start the server — only required once you set up txAdmin and want players to connect. Get one from https://keymaster.fivem.net when you're ready." },
            new() { Key = "buildChannel", Label = "FXServer build channel", FieldType = ConfigFieldType.Dropdown,
                    DefaultValue = "recommended", Options = ["recommended", "latest"],
                    Description = "Recommended = stable, what Cfx.re currently recommends. Latest = newest features, can be buggy." },
        ]);
        return fields;
    }
}
