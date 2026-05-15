using WGS.Models;

namespace WGS.Games;

public class TheIslePlugin : GamePluginBase
{
    public override string GameId          => "theisle";
    public override string GameName        => "The Isle";
    public override string Description     => "Dinosaur survival on a massive open island";
    public override string Category        => "Survival";
    public override int    SteamAppId       => 412680;
    public override int    GameStoreAppId   => 376210;
    public override string SteamBranch      => "evrima";
    public override string Executable       => @"TheIsle\Binaries\Win64\TheIsleServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 27020;
    public override int    DefaultMaxPlayers => 75;

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "map", "Isle_V3");
        return $"{map}?listen?Port={s.ServerPort}?QueryPort={s.QueryPort} " +
               $"?MaxPlayers={s.MaxPlayers}?ServerName=\"{s.ServerName}\"" +
               $"?ServerPassword=\"{s.ServerPassword}\"" +
               " -server -log -NotQuitOnCriticalError";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"] = "Gateway",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map", Label = "Kartta", FieldType = ConfigFieldType.Dropdown, DefaultValue = "Gateway",
                    Options = ["Gateway","Isle_V3","TheIsle_NightV1","TheIsle_Lost"] },
        ]);
        return fields;
    }
}
