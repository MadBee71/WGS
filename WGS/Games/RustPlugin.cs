using WGS.Models;

namespace WGS.Games;

public class RustPlugin : GamePluginBase
{
    public override string GameId        => "rust";
    public override string GameName      => "Rust";
    public override string Description   => "Multiplayer survival with base building and PvP";
    public override string Category      => "Survival";
    public override int    SteamAppId    => 258550;
    public override int    GameStoreAppId => 252490;
    public override string Executable    => "RustDedicated.exe";
    public override int    DefaultPort   => 28015;
    public override int    DefaultQueryPort => 28016;
    public override int    DefaultMaxPlayers => 100;
    public override bool   HasRcon       => true;

    public override string BuildStartArguments(GameServer s)
    {
        var seed      = S(s, "seed", "12345");
        var worldSz   = S(s, "worldSize", "3000");
        var tickrate  = S(s, "tickRate", "30");
        var desc      = S(s, "description", "A Rust Server");
        var identity  = S(s, "identity", "server1");

        var args = $"-batchmode +server.ip {s.ServerIp} +server.port {s.ServerPort} " +
                   $"+server.tickrate {tickrate} +server.hostname \"{s.ServerName}\" " +
                   $"+server.maxplayers {s.MaxPlayers} +server.worldsize {worldSz} " +
                   $"+server.seed {seed} +server.identity \"{identity}\" " +
                   $"+server.description \"{desc}\" +rcon.web 1";

        if (s.RconPort > 0)
            args += $" +rcon.port {s.RconPort}";
        if (!string.IsNullOrWhiteSpace(s.RconPassword))
            args += $" +rcon.enabled 1 +rcon.password \"{s.RconPassword}\"";

        return args;
    }

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["seed"]        = "12345",
        ["worldSize"]   = "3000",
        ["tickRate"]    = "30",
        ["description"] = "A Rust Server",
        ["identity"]    = "server1",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "seed",        Label = "World seed",   FieldType = ConfigFieldType.Number, DefaultValue = "12345" },
            new() { Key = "worldSize",   Label = "World size",   FieldType = ConfigFieldType.Slider, DefaultValue = "3000", Min = 1000, Max = 6000 },
            new() { Key = "tickRate",    Label = "Tick rate",    FieldType = ConfigFieldType.Slider, DefaultValue = "30", Min = 5, Max = 60 },
            new() { Key = "description", Label = "Description",  FieldType = ConfigFieldType.Text,   DefaultValue = "A Rust Server" },
            new() { Key = "identity",    Label = "Server identity (save folder)", FieldType = ConfigFieldType.Text, DefaultValue = "server1" },
        ]);
        return fields;
    }
}
