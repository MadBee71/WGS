using WGS.Models;

namespace WGS.Games;

public class TModLoaderPlugin : GamePluginBase
{
    public override string GameId          => "tmodloader";
    public override string GameName        => "tModLoader";
    public override string Description     => "⚠ Requires Terraria's Content folder copied in BEFORE starting (see World name field) — Terraria's official mod loader with its own dedicated server";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 0; // free, open source — downloaded directly from GitHub releases
    public override string Executable      => "start-tModLoaderServer.bat";
    public override int    DefaultPort     => 7777;
    public override int    DefaultQueryPort => 7777;
    public override int    DefaultMaxPlayers => 8;

    public override Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
        => Task.FromResult<(string, string)?>(("latest", "https://github.com/tModLoader/tModLoader/releases/latest/download/tModLoader.zip"));

    // tModLoader.zip contains only the loader itself, not Terraria's copyrighted game content.
    // The user must own Terraria and copy its Content folder in manually before first start —
    // WGS cannot obtain or redistribute copyrighted assets, and there is no reliable way to
    // detect its absence from here, so this is documented on the worldName field instead.
    public override string BuildStartArguments(GameServer s)
    {
        var world = S(s, "worldName", "World");
        var pass  = string.IsNullOrWhiteSpace(s.ServerPassword) ? "" : $" -password \"{s.ServerPassword}\"";
        return $"-port {s.ServerPort} -players {s.MaxPlayers}" +
               $" -world \"{s.InstallPath}\\Worlds\\{world}.wld\"" +
               $" -worldname \"{world}\" -autocreate 2 -secure{pass}";
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
            new() { Key = "worldName", Label = "World name", FieldType = ConfigFieldType.Text, DefaultValue = "World",
                    Description = "Requires Terraria's Content folder to be present in this server's install path — copy it from a legitimate Terraria install (Steam: steamapps\\common\\Terraria\\Content) before first start." },
        ]);
        return fields;
    }
}
