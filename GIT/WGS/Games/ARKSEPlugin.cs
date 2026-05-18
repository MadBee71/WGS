using WGS.Models;

namespace WGS.Games;

public class ARKSEPlugin : GamePluginBase
{
    public override string GameId          => "arkse";
    public override string GameName        => "ARK: Survival Evolved";
    public override string Description     => "Open world dinosaur survival";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 376030;
    public override int    GameStoreAppId  => 346110;
    public override string Executable      => @"ShooterGame\Binaries\Win64\ShooterGameServer.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 70;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s)
    {
        var map = S(s, "mapName", "TheIsland");
        var args = $"{map}?listen?SessionName=\"{s.ServerName}\"?MultiHome={s.ServerIp}?Port={s.ServerPort}?MaxPlayers={s.MaxPlayers}?QueryPort={s.QueryPort}";
        if (!string.IsNullOrWhiteSpace(S(s, "serverPassword")))
            args += $"?ServerPassword={S(s, "serverPassword")}";
        args += " -server -log";
        return args;
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mapName"]        = "TheIsland",
        ["serverPassword"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "mapName", Label = "Map", FieldType = ConfigFieldType.Dropdown, DefaultValue = "TheIsland",
                Options = ["TheIsland", "TheCenter", "ScorchedEarth_P", "Ragnarok", "Aberration_P", "Extinction", "Genesis", "CrystalIsles", "Gen2"] },
            new() { Key = "serverPassword", Label = "Server Password", FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
