using WGS.Models;

namespace WGS.Games;

public class UnturnedPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "unturned";
    public override string GameName        => "Unturned";
    public override string Description     => "Free-to-play zombie survival with crafting and building";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1110390;
    public override int    GameStoreAppId  => 304930;
    public override int    WorkshopAppId   => 304930;

    public string ModTargetDirectory => @"Modules";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => "Unturned.exe";
    public override int    DefaultPort     => 27015;
    public override int    DefaultQueryPort => 27016;
    public override int    DefaultMaxPlayers => 24;
    public override bool   HasRcon         => true;

    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s)
    {
        var identity = S(s, "identity", "MyServer");
        return $"-nographics -batchmode +secureserver/{identity}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["identity"] = "MyServer",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "identity", Label = "Server name (folder)", FieldType = ConfigFieldType.Text, DefaultValue = "MyServer" },
        ]);
        return fields;
    }
}
