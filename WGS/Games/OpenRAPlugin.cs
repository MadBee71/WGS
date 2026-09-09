using WGS.Models;

namespace WGS.Games;

public class OpenRAPlugin : GamePluginBase
{
    public override string GameId          => "openra";
    public override string GameName        => "OpenRA";
    public override string Description     => "Open-source engine remake of classic Command & Conquer, Red Alert and Dune 2000";
    public override string Category        => "Simulation";
    public override int    SteamAppId      => 0; // free, open source — installed via the official Windows installer, downloaded manually
    public override string Executable      => "OpenRA.Server.exe";
    public override int    DefaultPort     => 1234;
    public override int    DefaultQueryPort => 1234;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
    {
        var mod  = S(s, "mod", "ra");
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS OpenRA Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        var passArg = string.IsNullOrWhiteSpace(pass) ? "" : $" Server.Password={pass}";
        return $"Game.Mod={mod} Server.Name=\"{name}\" Server.ListenPort={s.ServerPort} " +
               $"Server.AdvertiseOnline=False Server.EnableSingleplayer=True{passArg}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["mod"] = "ra",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "mod", Label = "Mod", FieldType = ConfigFieldType.Dropdown, DefaultValue = "ra",
                    Options = ["ra", "cnc", "d2k"],
                    Description = "ra = Red Alert, cnc = Tiberian Dawn, d2k = Dune 2000." },
        ]);
        return fields;
    }
}
