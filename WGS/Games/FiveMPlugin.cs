using System.IO;
using WGS.Models;

namespace WGS.Games;

public class FiveMPlugin : GamePluginBase
{
    public override string GameId           => "fivem";
    public override string GameName         => "Grand Theft Auto V (FiveM)";
    public override string Description      => "FiveM multiplayer modification framework for GTA V — manual FXServer install required";
    public override string Category         => "Open World";
    public override int    SteamAppId       => 0;
    public override int    GameStoreAppId   => 271590; // GTA V's own Steam app — used for the cover image only
    public override string Executable       => "FXServer.exe";
    public override int    DefaultPort      => 30120;
    public override int    DefaultQueryPort => 30120;
    public override int    DefaultMaxPlayers => 32;
    public override bool   HasRcon          => true;

    public override Task<string?> GetManualDownloadUrlAsync() => CfxArtifactHelper.GetLatestServerDownloadUrlAsync();

    public override Task PreStartAsync(GameServer s)
    {
        var cfgPath = Path.Combine(s.InstallPath, "server.cfg");
        WriteConfigIfMissing(cfgPath, BuildServerCfg(s));
        return Task.CompletedTask;
    }

    private static string BuildServerCfg(GameServer s)
    {
        var licenseKey   = s.GameSpecificSettings.TryGetValue("licenseKey",   out var lk)  ? lk  : "YOUR_LICENSE_KEY";
        var txAdminPort  = s.GameSpecificSettings.TryGetValue("txAdminPort",  out var tap) ? tap : "40120";

        return
            $"""
            sv_licenseKey "{licenseKey}"
            sv_maxclients {s.MaxPlayers}
            sv_hostname "{s.ServerName}"
            sv_projectName "{s.ServerName}"

            endpoint_add_tcp "0.0.0.0:{s.ServerPort}"
            endpoint_add_udp "0.0.0.0:{s.ServerPort}"

            set txAdminPort {txAdminPort}

            set onesync on

            # Uncommented automatically by WGS — RCON requires a non-empty password to actually enable it.
            set rcon_password "{s.RconPassword}"

            # Default resources bundled with every FXServer build — without these, nothing loads
            # at all (no map, no chat, no spawning), matching Cfx.re's own example server.cfg.
            ensure mapmanager
            ensure chat
            ensure spawnmanager
            ensure sessionmanager
            ensure basic-gamemode
            ensure hardcap
            ensure rconlog

            # Add your own resources below
            # ensure your-resource-name
            """;
    }

    public override string BuildStartArguments(GameServer s)
    {
        return $"+exec server.cfg";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["licenseKey"]  = "",
        ["txAdminPort"] = "40120",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "licenseKey",  Label = "CFX License Key",  FieldType = ConfigFieldType.Password, DefaultValue = "",
                    Description = "Get your key from https://keymaster.fivem.net" },
            new() { Key = "txAdminPort", Label = "txAdmin Port",      FieldType = ConfigFieldType.Number,   DefaultValue = "40120", Min = 1024, Max = 65535 },
        ]);
        return fields;
    }
}
