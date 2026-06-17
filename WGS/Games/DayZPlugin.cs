using System.IO;
using WGS.Models;
using WGS.Services;

namespace WGS.Games;

public class DayZPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId           => "dayz";
    public override string GameName         => "DayZ";
    public override string Description      => "Post-apocalyptic open-world zombie survival";
    public override string Category         => "Survival";
    public override int    SteamAppId       => 223350;
    public override int    GameStoreAppId   => 221100;
    public override int    WorkshopAppId    => 221100;
    public override string Executable       => "DayZServer_x64.exe";
    public override int    DefaultPort      => 2302;
    public override int    DefaultQueryPort => 2303;
    public override int    DefaultMaxPlayers => 60;
    public override bool   HasRcon          => true;
    public override bool   RequiresSteamLogin => true;
    // BattlEye RCON global broadcast (-1 = all players)
    public override string? GetBroadcastCommand(string message) => $"say -1 \"{message}\"";

    // IWorkshopPlugin
    public string ModTargetDirectory => string.Empty;

    public Task OnModDownloadedAsync(string serverInstallPath, string workshopItemPath, ulong modId, string modName)
        => GroupAHelper.OnModDownloadedAsync(serverInstallPath, workshopItemPath, modId);

    public Task OnModRemovedAsync(string serverInstallPath, string workshopItemPath, ulong modId, string modName)
        => GroupAHelper.OnModRemovedAsync(serverInstallPath, workshopItemPath, modId);

    public string BuildModArguments(IReadOnlyList<ulong> activeModIds, string serverInstallPath)
        => GroupAHelper.BuildModArguments(activeModIds);

    public override string BuildStartArguments(GameServer s)
    {
        var cfg      = S(s, "configFile", "serverDZ.cfg");
        var profiles = S(s, "profiles", "profiles");

        var args = $"-config={cfg} -port={s.ServerPort} " +
                   $"-profiles=\"{s.InstallPath}\\{profiles}\" " +
                   $"-dologs -adminlog -freezecheck";

        var workshopMods = S(s, "__wgsWorkshopMods", ""); // set by ServerViewModel before start
        var manualMods   = S(s, "mods", "");
        var allMods = string.Join(";", new[] { workshopMods, manualMods }.Where(m => !string.IsNullOrWhiteSpace(m)));
        if (!string.IsNullOrWhiteSpace(allMods))
            args += $" \"-mod={allMods}\"";

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
            new() { Key = "mods",       Label = "Extra mods (manual, ;-sep)",          FieldType = ConfigFieldType.Text, DefaultValue = "" },
        ]);
        return fields;
    }
}
