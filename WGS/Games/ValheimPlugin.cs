using WGS.Models;

namespace WGS.Games;

public class ValheimPlugin : GamePluginBase, IWorkshopPlugin
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

    public override string? ValidateBeforeStart(GameServer server)
        => string.IsNullOrEmpty(server.ServerPassword) || server.ServerPassword.Length < 5
            ? "Valheim requires a server password of at least 5 characters, or the server will reject all connections. Set one in Settings → Password."
            : null;

    public override string BuildStartArguments(GameServer s)
    {
        var name   = S(s, "serverName", "MyValheim");
        var world  = S(s, "worldName", "Dedicated");
        var pass   = s.ServerPassword;
        var cross  = S(s, "crossplay", "false") == "true" ? "-crossplay" : "";
        var pub    = S(s, "public", "true") == "true" ? "1" : "0";
        return $"-nographics -batchmode -name \"{name}\" -world \"{world}\" -password \"{pass}\" -port {s.ServerPort} -savedir \"{s.InstallPath}\\saves\" -public {pub} {cross}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverName"] = "MyValheim",
        ["worldName"]  = "Dedicated",
        ["crossplay"]  = "false",
        ["public"]     = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverName", Label = "Server name", FieldType = ConfigFieldType.Text,   DefaultValue = "MyValheim" },
            new() { Key = "worldName",  Label = "World name",  FieldType = ConfigFieldType.Text,   DefaultValue = "Dedicated" },
            new() { Key = "crossplay",  Label = "Crossplay",      FieldType = ConfigFieldType.Toggle, DefaultValue = "false" },
            new() { Key = "public",     Label = "Public listing",  FieldType = ConfigFieldType.Toggle, DefaultValue = "true" },
        ]);
        return fields;
    }
}
