using WGS.Models;

namespace WGS.Games;

public class ProjectZomboidPlugin : GamePluginBase
{
    public override string GameId          => "projectzomboid";
    public override string GameName        => "Project Zomboid";
    public override string Description     => "Hardcore zombie survival RPG with deep simulation";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 380870;
    public override int    GameStoreAppId  => 108600;
    public override string Executable      => "ProjectZomboidServer.bat";
    public override int    DefaultPort     => 16261;
    public override int    DefaultQueryPort => 16262;
    public override int    DefaultMaxPlayers => 32;
    public override bool   HasRcon         => true;

    public override string BuildStartArguments(GameServer s)
    {
        var identity = S(s, "identity", "servertest");
        return $"-servername {identity}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["identity"] = "servertest",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "identity", Label = "Server profile name", FieldType = ConfigFieldType.Text, DefaultValue = "servertest" },
        ]);
        return fields;
    }
}
