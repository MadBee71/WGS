using WGS.Models;

namespace WGS.Games;

public class LongvinterPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "longvinter";
    public override string GameName        => "Longvinter";
    public override string Description     => "Multiplayer open-world survival on snowy islands";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1639880;
    public override int    GameStoreAppId  => 1634960;
    public override int    WorkshopAppId   => 1639880;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"Longvinter\Binaries\Win64\LongvinterServer-Win64-Shipping.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 32;
    public override string BuildStartArguments(GameServer s)
        => $"LongvinterServer -Port={s.ServerPort} -QueryPort={s.QueryPort} " +
           $"-MaxPlayers={s.MaxPlayers}";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverTag"] = "none",
        ["communityWebsite"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverTag",         Label = "Server tag",      FieldType = ConfigFieldType.Text, DefaultValue = "none" },
            new() { Key = "communityWebsite",   Label = "Community URL",   FieldType = ConfigFieldType.Text, DefaultValue = "" },
        ]);
        return fields;
    }
}
