using WGS.Models;

namespace WGS.Games;

public class DayZPlugin : GamePluginBase
{
    public override string GameId           => "dayz";
    public override string GameName         => "DayZ";
    public override string Description      => "Post-apocalyptic open-world zombie survival";
    public override string Category         => "Survival";
    public override int    SteamAppId       => 223350;
    public override int    GameStoreAppId   => 221100;
    public override string Executable       => "DayZServer_x64.exe";
    public override int    DefaultPort      => 2302;
    public override int    DefaultQueryPort => 2303;
    public override int    DefaultMaxPlayers => 60;
    public override bool   HasRcon          => true;
    public override bool   RequiresSteamLogin => true;

    public override string BuildStartArguments(GameServer s)
    {
        var cfg      = S(s, "configFile", "serverDZ.cfg");
        var profiles = S(s, "profiles", "profiles");
        var mods     = S(s, "mods", "");

        var args = $"-config={cfg} -port={s.ServerPort} " +
                   $"-profiles=\"{s.InstallPath}\\{profiles}\" " +
                   $"-dologs -adminlog -freezecheck";

        if (!string.IsNullOrWhiteSpace(mods))
            args += $" \"-mod={mods}\"";

        return args;
    }

    public override string? GetStopCommand(GameServer server) => "#shutdown";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["configFile"] = "serverDZ.cfg",
        ["profiles"]   = "profiles",
        ["mods"]       = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "configFile", Label = "Config file",                         FieldType = ConfigFieldType.Text, DefaultValue = "serverDZ.cfg" },
            new() { Key = "profiles",   Label = "Profiles folder (logs & saves)",      FieldType = ConfigFieldType.Text, DefaultValue = "profiles" },
            new() { Key = "mods",       Label = "Mods (;-sep, e.g. @CF;@VPPAdmin)",    FieldType = ConfigFieldType.Text, DefaultValue = "" },
        ]);
        return fields;
    }
}
