using WGS.Models;

namespace WGS.Games;

public class TerrariaPlugin : GamePluginBase
{
    public override string GameId          => "terraria";
    public override string GameName        => "Terraria";
    public override string Description     => "2D sandbox adventure — download TerrariaServer.exe from terraria.org";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 0;   // Not on Steam; install manually from terraria.org/api/download/pc-dedicated-server
    public override string Executable      => "TerrariaServer.exe";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 7777;
    public override int    DefaultMaxPlayers => 8;

    public override string BuildStartArguments(GameServer s)
    {
        var world = S(s, "worldName", "World");
        var pass  = string.IsNullOrWhiteSpace(s.ServerPassword) ? "" : $" -password \"{s.ServerPassword}\"";
        return $"-port {s.ServerPort} -players {s.MaxPlayers}" +
               $" -world \"{s.InstallPath}\\Worlds\\{world}.wld\"" +
               $" -worldname \"{world}\" -secure{pass}";
    }

    public override string? GetStopCommand(GameServer server) => "exit";

    public override Dictionary<string, string> GetDefaultSettings() => new()
    {
        ["worldName"] = "World",
    };

    public override List<ConfigField> GetConfigFields()
    {
        var fields = BaseFields();
        fields.AddRange([
            new() { Key = "worldName", Label = "Maailman nimi", FieldType = ConfigFieldType.Text, DefaultValue = "World" },
        ]);
        return fields;
    }
}
