using WGS.Models;

namespace WGS.Games;

public class WindRosePlugin : GamePluginBase
{
    public override string GameId          => "windrose";
    public override string GameName        => "Windrose";
    public override string Description     => "Sail, explore and survive in an open-world ocean";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 4129620;
    public override int    GameStoreAppId  => 3041230;
    public override string Executable      => @"R5\Binaries\Win64\WindroseServer-Win64-Shipping.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;

    public override string BuildStartArguments(GameServer s)
    {
        var extra = S(s, "extraArgs");
        return $"-port={s.ServerPort} -queryport={s.QueryPort} -maxplayers={s.MaxPlayers} -log -nostatustext" +
               (string.IsNullOrWhiteSpace(extra) ? "" : " " + extra);
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["extraArgs"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.Add(new() { Key = "extraArgs", Label = "Extra arguments", FieldType = ConfigFieldType.Text, DefaultValue = "" });
        return fields;
    }
}
