using WGS.Models;

namespace WGS.Games;

public class FiveMEnhancedPlugin : GamePluginBase
{
    public override string GameId            => "fivem_enhanced";
    public override string GameName          => "Grand Theft Auto V Enhanced (FiveM)";
    public override string Description       => "FiveM for GTAV Enhanced — uses cfx-server.exe (early access). Server artifacts downloaded from Cfx.re.";
    public override string Category          => "Open World";
    public override int    SteamAppId        => 0;
    public override int    GameStoreAppId    => 271590;
    public override string Executable        => "cfx-server.exe";
    public override int    DefaultPort       => 30120;
    public override int    DefaultQueryPort  => 30120;
    public override int    DefaultMaxPlayers => 32;
    public override bool   HasRcon           => true;
    public override bool   SupportsVersionCheck => true;
    public override string EngineFamily      => "fivem";

    public override async Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
    {
        var useLatest = S(server, "buildChannel", "recommended") == "latest";
        var info = useLatest ? await CfxEnhancedArtifactHelper.GetLatestAsync() : await CfxEnhancedArtifactHelper.GetRecommendedAsync();
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
        var recommended = await CfxEnhancedArtifactHelper.GetRecommendedAsync();
        var latest      = await CfxEnhancedArtifactHelper.GetLatestAsync();
        return (recommended?.Build, latest?.Build);
    }

    public override Task PreStartAsync(GameServer s) => Task.CompletedTask;

    public override string? GetStopCommand(GameServer server) => "quit";

    public override string BuildStartArguments(GameServer s)
    {
        var serverProfile = S(s, "serverProfile", "default");
        var txAdminPort   = S(s, "txAdminPort",   "40120");
        return $"+set serverProfile \"{serverProfile}\" +set txAdminPort \"{txAdminPort}\"";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverProfile"] = "default",
        ["buildChannel"]  = "recommended",
        ["txAdminPort"]   = "40120",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverProfile", Label = "TxAdmin Server Profile", FieldType = ConfigFieldType.Text, DefaultValue = "default",
                    Description = "Server profile to load via txAdmin." },
            new() { Key = "txAdminPort",   Label = "TxAdmin Port",           FieldType = ConfigFieldType.Text, DefaultValue = "40120",
                    Description = "Port txAdmin listens on. Default is 40120." },
            new() { Key = "buildChannel",  Label = "Server build channel",   FieldType = ConfigFieldType.Dropdown,
                    DefaultValue = "recommended", Options = ["recommended", "latest"],
                    Description = "Recommended = stable. Latest = newest features, may be unstable. CFX license key and all other settings are configured inside txAdmin after first launch." },
        ]);
        return fields;
    }
}
