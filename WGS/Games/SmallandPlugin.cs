using WGS.Models;

namespace WGS.Games;

public class SmallandPlugin : GamePluginBase
{
    public override string GameId          => "smalland";
    public override string GameName        => "Smalland: Survive the Wilds";
    public override string Description     => "Co-op survival game where players are shrunk to insect size";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 808040;
    public override int    GameStoreAppId  => 768200;
    public override string Executable      => @"SMALLAND\Binaries\Win64\SMALLANDServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7778;
    public override int    DefaultMaxPlayers => 8;

    // Public EOS credentials — same for all servers, from the official start-server.bat.
    private const string EosDeploymentId         = "50f2b148496e4cbbbdeefbecc2ccd6a3";
    private const string EosDedicatedClientId     = "xyza78918KT08TkA6emolUay8yhvAAy2";
    private const string EosDedicatedClientSecret = "aN2GtVw7aHb6hx66HwohNM+qktFaO3vtrLSbGdTzZWk";

    public override string? GetWorkingDirectory(GameServer s) => s.InstallPath;

    public override string BuildStartArguments(GameServer s)
    {
        var raw        = S(s, "serverName", "");
        var serverName = (!string.IsNullOrWhiteSpace(raw) && raw != "My Server") ? raw :
                         !string.IsNullOrWhiteSpace(s.ServerName) ? s.ServerName :
                         !string.IsNullOrWhiteSpace(s.DisplayName) ? s.DisplayName : "My Server";
        var worldName  = S(s, "worldName", "World");

        var map = $"/Game/Maps/WorldGame/WorldGame_Smalland?SERVERNAME={serverName}?WORLDNAME={worldName}";

        // Password — only append if set
        if (!string.IsNullOrWhiteSpace(s.ServerPassword))
            map += $"?PASSWORD={s.ServerPassword}";

        // Boolean flags — only append if enabled
        if (S(s, "friendlyFire",           "0") == "1") map += "?FRIENDLYFIRE";
        if (S(s, "peacefulMode",           "0") == "1") map += "?PEACEFULMODE";
        if (S(s, "keepInventory",          "0") == "1") map += "?KEEPINVENTORY";
        if (S(s, "noDeterioration",        "0") == "1") map += "?NODETERIORATION";
        if (S(s, "tamedCreaturesImmortal", "0") == "1") map += "?TAMEDCREATURESIMMORTAL";
        if (S(s, "private",                "0") == "1") map += "?PRIVATE";
        if (S(s, "crossplay",              "1") == "1") map += "?CROSSPLAY";

        // Numeric settings
        map += $"?lengthofdayseconds={S(s, "lengthOfDaySeconds",            "1800")}";
        map += $"?lengthofseasonseconds={S(s, "lengthOfSeasonSeconds",      "10800")}";
        map += $"?creaturehealthmodifier={S(s, "creatureHealthModifier",     "100")}";
        map += $"?creaturedamagemodifier={S(s, "creatureDamageModifier",     "100")}";
        map += $"?creaturerespawnratemodifier={S(s, "creatureRespawnRate",   "100")}";
        map += $"?resourcerespawnratemodifier={S(s, "resourceRespawnRate",   "100")}";
        map += $"?creaturespawnchancemodifier={S(s, "creatureSpawnChance",   "100")}";
        map += $"?craftingtimemodifier={S(s, "craftingTimeModifier",         "100")}";
        map += $"?craftingfuelmodifier={S(s, "craftingFuelModifier",         "100")}";
        map += $"?stormfrequencymodifier={S(s, "stormFrequencyModifier",     "100")}";
        map += $"?nourishmentlossmodifier={S(s, "nourishmentLossModifier",   "100")}";
        map += $"?falldamagemodifier={S(s, "fallDamageModifier",             "100")}";
        map += "?SESSIONPLATFORM=pc";

        return $"\"{map}\"" +
               $" -ini:Engine:[EpicOnlineServices]:DeploymentId={EosDeploymentId}" +
               $" -ini:Engine:[EpicOnlineServices]:DedicatedServerClientId={EosDedicatedClientId}" +
               $" -ini:Engine:[EpicOnlineServices]:DedicatedServerClientSecret={EosDedicatedClientSecret}" +
               $" -port={s.ServerPort} -NOSTEAM -log";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverName"]               = "",
        ["worldName"]                = "World",
        ["friendlyFire"]             = "0",
        ["peacefulMode"]             = "0",
        ["keepInventory"]            = "0",
        ["noDeterioration"]          = "0",
        ["tamedCreaturesImmortal"]   = "0",
        ["private"]                  = "0",
        ["crossplay"]                = "1",
        ["lengthOfDaySeconds"]       = "1800",
        ["lengthOfSeasonSeconds"]    = "10800",
        ["creatureHealthModifier"]   = "100",
        ["creatureDamageModifier"]   = "100",
        ["creatureRespawnRate"]      = "100",
        ["resourceRespawnRate"]      = "100",
        ["creatureSpawnChance"]      = "100",
        ["craftingTimeModifier"]     = "100",
        ["craftingFuelModifier"]     = "100",
        ["stormFrequencyModifier"]   = "100",
        ["nourishmentLossModifier"]  = "100",
        ["fallDamageModifier"]       = "100",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverName",             Label = "Server Name",                  FieldType = ConfigFieldType.Text,     DefaultValue = "",      Description = "Server name in the browser. Leave empty to use the WGS server name." },
            new() { Key = "worldName",              Label = "World Name",                   FieldType = ConfigFieldType.Text,     DefaultValue = "World", Description = "Name of the world/save file." },
            new() { Key = "friendlyFire",           Label = "Friendly Fire (PvP)",          FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",     Options = ["0","1"], Description = "Allow players to damage each other." },
            new() { Key = "peacefulMode",           Label = "Peaceful Mode",                FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",     Options = ["0","1"], Description = "No hostile creatures." },
            new() { Key = "keepInventory",          Label = "Keep Inventory on Death",      FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",     Options = ["0","1"] },
            new() { Key = "noDeterioration",        Label = "No Building Deterioration",    FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",     Options = ["0","1"] },
            new() { Key = "tamedCreaturesImmortal", Label = "Tamed Creatures Immortal",     FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",     Options = ["0","1"] },
            new() { Key = "private",                Label = "Private Server",               FieldType = ConfigFieldType.Dropdown, DefaultValue = "0",     Options = ["0","1"], Description = "Hide from server browser." },
            new() { Key = "crossplay",              Label = "Crossplay",                    FieldType = ConfigFieldType.Dropdown, DefaultValue = "1",     Options = ["1","0"], Description = "Show server to other platforms." },
            new() { Key = "lengthOfDaySeconds",     Label = "Day Length (seconds)",         FieldType = ConfigFieldType.Text,     DefaultValue = "1800",  Description = "Default 1800 = 30 min." },
            new() { Key = "lengthOfSeasonSeconds",  Label = "Season Length (seconds)",      FieldType = ConfigFieldType.Text,     DefaultValue = "10800", Description = "Default 10800 = 3 hours." },
            new() { Key = "creatureHealthModifier", Label = "Creature Health %",            FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "20–300." },
            new() { Key = "creatureDamageModifier", Label = "Creature Damage %",            FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "20–300." },
            new() { Key = "creatureRespawnRate",    Label = "Creature Respawn Rate %",      FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "10–1000." },
            new() { Key = "resourceRespawnRate",    Label = "Resource Respawn Rate %",      FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "10–1000." },
            new() { Key = "creatureSpawnChance",    Label = "Creature Spawn Chance %",      FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "10–1000." },
            new() { Key = "craftingTimeModifier",   Label = "Crafting Time %",              FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "10–1000." },
            new() { Key = "craftingFuelModifier",   Label = "Crafting Fuel %",              FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "10–1000." },
            new() { Key = "stormFrequencyModifier", Label = "Storm Frequency %",            FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "10–1000." },
            new() { Key = "nourishmentLossModifier",Label = "Nourishment Loss %",           FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "0–100." },
            new() { Key = "fallDamageModifier",     Label = "Fall Damage %",                FieldType = ConfigFieldType.Text,     DefaultValue = "100",   Description = "50–100." },
        ]);
        return fields;
    }
}
