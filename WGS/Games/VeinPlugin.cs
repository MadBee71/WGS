using WGS.Models;

namespace WGS.Games;

public class VeinPlugin : GamePluginBase
{
    public override string GameId          => "vein";
    public override string GameName        => "Vein";
    public override string Description     => "Open world survival dedicated server";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 2131400;
    public override int    GameStoreAppId  => 2131400;
    public override string Executable      => @"Vein\Binaries\Win64\VeinServer-Win64-Test.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 16;
    public override bool   HasRcon         => true;
    public override string BuildStartArguments(GameServer s)
        => $"-Port={s.ServerPort} -QueryPort={s.QueryPort} -multihome={s.ServerIp} -log";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverPassword"] = "",
        ["adminPassword"]  = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverPassword", Label = "Server Password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
            new() { Key = "adminPassword",  Label = "Admin Password",  FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
