using WGS.Models;

namespace WGS.Games;

public class ValheimPlugin : GamePluginBase
{
    public override string GameId        => "valheim";
    public override string GameName      => "Valheim";
    public override string Description   => "Viking survival co-op up to 10 players";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 896660;
    public override int    GameStoreAppId => 892970;
    public override string Executable    => "valheim_server.exe";
    public override int    DefaultPort   => 2456;
    public override int    DefaultQueryPort => 2457;
    public override int    DefaultMaxPlayers => 10;

    public override string BuildStartArguments(GameServer s)
    {
        var name  = S(s, "serverName", "MyValheim");
        var world = S(s, "worldName", "Dedicated");
        var pass  = s.ServerPassword;
        var cross = S(s, "crossplay", "false") == "true" ? "-crossplay" : "";
        return $"-nographics -batchmode -name \"{name}\" -world \"{world}\" -password \"{pass}\" -port {s.ServerPort} -savedir \"{s.InstallPath}\\saves\" {cross}";
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["serverName"] = "MyValheim",
        ["worldName"]  = "Dedicated",
        ["crossplay"]  = "false",
        ["public"]     = "true",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName",  Label = "Maailman nimi",  FieldType = ConfigFieldType.Text,   DefaultValue = "Dedicated" },
            new() { Key = "crossplay",  Label = "Crossplay",      FieldType = ConfigFieldType.Toggle, DefaultValue = "false" },
            new() { Key = "public",     Label = "Julkinen lista",  FieldType = ConfigFieldType.Toggle, DefaultValue = "true" },
        ]);
        return fields;
    }
}
