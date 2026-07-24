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
    // FXServer doesn't speak Source RCON — it uses a legacy UDP protocol on the game port itself.
    public override string EngineFamily     => "fivem";

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

    public override string? GetStopCommand(GameServer server) => "quit";

    public override string BuildStartArguments(GameServer s)
    {
        var txDataPath = S(s, "TXHOST_DATA_PATH", "....txData");
        var txaPort    = S(s, "TXHOST_TXA_PORT",  "40120");
        return $"+set TXHOST_DATA_PATH \"{txDataPath}\" +set TXHOST_TXA_PORT \"{txaPort}\"";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["TXHOST_DATA_PATH"] = "....txData",
        ["TXHOST_TXA_PORT"]  = "40120",
        ["buildChannel"]     = "recommended",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "TXHOST_DATA_PATH", Label = "TxAdmin Data Path", FieldType = ConfigFieldType.Text, DefaultValue = "....txData",
                    Description = "TxAdmin server data path (TXHOST_DATA_PATH)." },
            new() { Key = "TXHOST_TXA_PORT",  Label = "TxAdmin Port",      FieldType = ConfigFieldType.Text, DefaultValue = "40120",
                    Description = "Port txAdmin listens on. Default is 40120. Change if running FiveM and RedM side-by-side to avoid conflicts." },
            new() { Key = "buildChannel",      Label = "FXServer build channel", FieldType = ConfigFieldType.Dropdown,
                    DefaultValue = "recommended", Options = ["recommended", "latest"],
                    Description = "Recommended = stable, what Cfx.re currently recommends. Latest = newest features, can be buggy. The CFX license key, RCON password and everything else are set inside txAdmin itself after first launch — not here." },
        ]);
        return fields;
    }
}
