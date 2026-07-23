using WGS.Models;

namespace WGS.Games;

public class SmallandPlugin : GamePluginBase
{
    public override string GameId          => "smalland";
    public override string GameName        => "Smalland: Survive the Wilds";
    public override string Description     => "Co-op survival game where players are shrunk to insect size";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 808040; // dedicated server — different from the game's own appid (768200)
    public override int    GameStoreAppId  => 768200;
    public override string Executable      => @"SMALLAND\Binaries\Win64\SMALLANDServer-Win64-Shipping.exe";
    public override int    DefaultPort      => 7777;
    public override int    DefaultQueryPort => 7778;
    public override int    DefaultMaxPlayers => 8;

    // Smalland dedicated server EOS credentials — public, same for all servers, from the official start-server.bat.
    private const string EosDeploymentId   = "50f2b148496e4cbbbdeefbecc2ccd6a3";
    private const string EosDedicatedClientId     = "xyza78918KT08TkA6emolUay8yhvAAy2";
    private const string EosDedicatedClientSecret = "aN2GtVw7aHb6hx66HwohNM+qktFaO3vtrLSbGdTzZWk";

    public override string? GetWorkingDirectory(GameServer s) => s.InstallPath;

    public override string BuildStartArguments(GameServer s)
    {
        var raw        = S(s, "serverName", "");
        var serverName = !string.IsNullOrWhiteSpace(raw) ? raw :
                         !string.IsNullOrWhiteSpace(s.ServerName) ? s.ServerName :
                         !string.IsNullOrWhiteSpace(s.DisplayName) ? s.DisplayName : "My Server";
        var worldName  = S(s, "worldName",  "World");
        var map = $"/Game/Maps/WorldGame/WorldGame_Smalland" +
                  $"?SERVERNAME={serverName}?WORLDNAME={worldName}?CROSSPLAY" +
                  $"?lengthofdayseconds=1800?lengthofseasonseconds=10800" +
                  $"?creaturehealthmodifier=100?creaturedamagemodifier=100" +
                  $"?creaturerespawnratemodifier=100?resourcerespawnratemodifier=100" +
                  $"?creaturespawnchancemodifier=100?craftingtimemodifier=100" +
                  $"?craftingfuelmodifier=100?stormfrequencymodifier=100" +
                  $"?nourishmentlossmodifier=100?falldamagemodifier=100?SESSIONPLATFORM=pc";
        return $"\"{map}\"" +
               $" -ini:Engine:[EpicOnlineServices]:DeploymentId={EosDeploymentId}" +
               $" -ini:Engine:[EpicOnlineServices]:DedicatedServerClientId={EosDedicatedClientId}" +
               $" -ini:Engine:[EpicOnlineServices]:DedicatedServerClientSecret={EosDedicatedClientSecret}" +
               $" -port={s.ServerPort} -NOSTEAM -log";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverName"] = "",
        ["worldName"]  = "World",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "serverName", Label = "Server Name",  FieldType = ConfigFieldType.Text, DefaultValue = "My Server",
                    Description = "Server name visible in the server browser. Leave empty to use the WGS server name." },
            new() { Key = "worldName",  Label = "World Name",   FieldType = ConfigFieldType.Text, DefaultValue = "World",
                    Description = "Name of the world/save file." },
        ]);
        return fields;
    }
}
