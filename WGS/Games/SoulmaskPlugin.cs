using WGS.Models;

namespace WGS.Games;

public class SoulmaskPlugin : GamePluginBase
{
    public override string GameId          => "soulmask";
    public override string GameName        => "Soulmask";
    public override string Description     => "Open-world tribal survival with NPC followers";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 3017310;
    public override int    GameStoreAppId  => 2646460;
    public override string Executable      => @"WS\Binaries\Win64\WSServer-Win64-Shipping.exe";
    public override int    DefaultPort     => 8777;
    public override int    DefaultQueryPort => 27015;
    public override int    DefaultMaxPlayers => 20;

    public override string BuildStartArguments(GameServer s)
    {
        var map       = S(s, "map",       "Level01_Main");
        var mode      = S(s, "gameMode",  "pve");
        var adminPass = S(s, "adminPass", "");
        var pass      = S(s, "serverPass", "");

        var args = $"{map} -server -log -UTF8Output " +
                   $"-Port={s.ServerPort} -QueryPort={s.QueryPort} " +
                   $"-SteamServerName=\"{s.ServerName}\" -MaxPlayers={s.MaxPlayers}" +
                   $" -{mode}";

        if (!string.IsNullOrWhiteSpace(pass))     args += $" -PSW=\"{pass}\"";
        if (!string.IsNullOrWhiteSpace(adminPass)) args += $" -adminpsw=\"{adminPass}\"";

        return args;
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["map"]       = "Level01_Main",
        ["gameMode"]  = "pve",
        ["adminPass"] = "",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "map",       Label = "Map",            FieldType = ConfigFieldType.Dropdown, DefaultValue = "Level01_Main",
                    Options = ["Level01_Main", "DLC_Level01_Main"],
                    Description = "Level01_Main = base game. DLC_Level01_Main = Civilizations DLC (requires owning the DLC)." },
            new() { Key = "gameMode",  Label = "Game mode",      FieldType = ConfigFieldType.Dropdown, DefaultValue = "pve", Options = ["pve", "pvp"] },
            new() { Key = "adminPass", Label = "Admin password",  FieldType = ConfigFieldType.Password, DefaultValue = "" },
        ]);
        return fields;
    }
}
