using WGS.Models;

namespace WGS.Games;

public class VRisingPlugin : GamePluginBase
{
    public override string GameId          => "vrising";
    public override string GameName        => "V Rising";
    public override string Description     => "Vampire survival with castle building";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 1829350;
    public override int    GameStoreAppId  => 1604030;
    public override string Executable      => "VRisingServer.exe";
    public override int    DefaultPort     => 9876;
    public override int    DefaultQueryPort => 9877;
    public override int    DefaultMaxPlayers => 40;
    protected override bool FilterUnityShaderNoise => true;

    public override string BuildStartArguments(GameServer s)
    {
        var saveName = S(s, "saveName", "world1");
        return $"-batchmode -nographics " +
               $"-persistentDataPath \"{s.InstallPath}\\Saves\" " +
               $"-serverName \"{s.ServerName}\" -description \"{S(s, "description", "")}\" " +
               $"-gamePort {s.ServerPort} -queryPort {s.QueryPort} " +
               $"-maxConnectedUsers {s.MaxPlayers} -saveName {saveName}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["saveName"]    = "world1",
        ["description"] = "V Rising Dedicated Server",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "saveName",    Label = "Save-nimi", FieldType = ConfigFieldType.Text, DefaultValue = "world1" },
            new() { Key = "description", Label = "Kuvaus",    FieldType = ConfigFieldType.Text, DefaultValue = "V Rising Dedicated Server" },
        ]);
        return fields;
    }
}
