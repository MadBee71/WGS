using WGS.Models;

namespace WGS.Games;

public class ASTRONEERPlugin : GamePluginBase, IWorkshopPlugin
{
    public override string GameId          => "astroneer";
    public override string GameName        => "ASTRONEER";
    public override string Description     => "Space exploration and base-building co-op";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 728470;
    public override int    GameStoreAppId  => 361420;
    public override int    WorkshopAppId   => 728470;

    public string ModTargetDirectory => @"Mods";
    public Task OnModDownloadedAsync(string s, string w, ulong id, string n) => GroupBHelper.OnModDownloadedAsync(s, w, id, ModTargetDirectory);
    public Task OnModRemovedAsync(string s, string w, ulong id, string n)    => GroupBHelper.OnModRemovedAsync(s, id, ModTargetDirectory);
    public string BuildModArguments(IReadOnlyList<ulong> ids, string _) => string.Empty;
    public override string Executable      => @"Astro\Binaries\Win64\AstroServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7777;
    public override int    DefaultMaxPlayers => 4;

    public override string BuildStartArguments(GameServer s)
        => $"-MultiplayerPort={s.ServerPort} -QueryPort={s.QueryPort} " +
           $"-ServerName=\"{s.ServerName}\" -ServerPassword=\"{s.ServerPassword}\"";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["saveName"] = "ASTRONEER",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "saveName", Label = "Save-nimi", FieldType = ConfigFieldType.Text, DefaultValue = "ASTRONEER" },
        ]);
        return fields;
    }
}
