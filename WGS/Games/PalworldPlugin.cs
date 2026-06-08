using WGS.Models;

namespace WGS.Games;

public class PalworldPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "palworld";
    public override string GameName        => "Palworld";
    public override string Description     => "Survival crafting with creature collecting";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2394010;
    public override int    GameStoreAppId  => 1623730;
    public override int    WorkshopAppId   => 2394010;

    public string ModTargetDirectory => @"Pal/Content/Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"Pal\Binaries\Win64\PalServer-Win64-Shipping-Cmd.exe";
    public override int    DefaultPort     => 8211;
    public override int    DefaultQueryPort => 8212;
    public override int    DefaultMaxPlayers => 32;
    public override string BuildStartArguments(GameServer s)
        => $"-port={s.ServerPort} -queryport={s.QueryPort} -players={s.MaxPlayers} " +
           $"EpicApp=PalServer -useperfthreads -NoAsyncLoadingThread -UseMultithreadForDS";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverDescription"] = "Palworld Server",
        ["adminPassword"]     = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverDescription", Label = "Description",          FieldType = ConfigFieldType.Text,     DefaultValue = "Palworld Server" },
            new() { Key = "adminPassword",      Label = "Admin password",  FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
