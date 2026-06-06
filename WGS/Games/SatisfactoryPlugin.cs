using WGS.Models;

namespace WGS.Games;

public class SatisfactoryPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "satisfactory";
    public override string GameName        => "Satisfactory";
    public override string Description     => "Factory building open-world game";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 1690800;
    public override int    GameStoreAppId  => 526870;
    public override int    WorkshopAppId   => 526870;

    public string ModTargetDirectory => @"FactoryGame/Content/Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"Engine\Binaries\Win64\FactoryServer-Win64-Shipping.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 15777;
    public override int    DefaultMaxPlayers => 4;

    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -ServerQueryPort={s.QueryPort} -multihome=0.0.0.0";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["autosaveInterval"] = "300",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "autosaveInterval", Label = "Autosave (s)", FieldType = ConfigFieldType.Number, DefaultValue = "300", Min = 60, Max = 3600 },
        ]);
        return fields;
    }
}
