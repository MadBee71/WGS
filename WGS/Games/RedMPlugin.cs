using System.IO;
using WGS.Models;

namespace WGS.Games;

public class RedMPlugin : GamePluginBase
{
    public override string GameId          => "redm";
    public override string GameName        => "Red Dead Redemption 2 (RedM)";
    public override string Description     => "Red Dead Redemption 2 multiplayer — download FXServer from cfx.re/redm";
    public override string Category        => "Open World";
    public override int    SteamAppId      => 0;   // Not on Steam; install manually from cfx.re/redm
    public override int    GameStoreAppId  => 1174180; // RDR2's own Steam app — used for the cover image only
    public override string Executable      => @"server\FXServer.exe";
    public override int    DefaultPort     => 30120;
    public override int    DefaultQueryPort => 30120;
    public override int    DefaultMaxPlayers => 32;

    public override Task<string?> GetManualDownloadUrlAsync() => CfxArtifactHelper.GetLatestServerDownloadUrlAsync();

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

            # Add resources below
            # ensure mapmanager
            # ensure chat
            # ensure spawnmanager
            """;
    }

    public override string BuildStartArguments(GameServer s)
        => $"+set citizen_dir \"{s.InstallPath}\\server\\citizen\" +exec server.cfg";

    public override string? GetStopCommand(GameServer server) => "quit";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["licenseKey"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "licenseKey", Label = "CFX License Key", FieldType = ConfigFieldType.Password, DefaultValue = "",
                            Description = "Get your key from https://keymaster.fivem.net" });
        return fields;
    }
}
