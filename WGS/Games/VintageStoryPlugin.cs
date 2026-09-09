using System.IO;
using WGS.Models;

namespace WGS.Games;

public class VintageStoryPlugin : GamePluginBase
{
    public override string GameId          => "vintagestory";
    public override string GameName        => "Vintage Story";
    public override string Description     => "⚠ Manual install required — download needs a Vintagestory.net account login, WGS can't automate it — deep, realistic survival sandbox with strong dedicated server support";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 0;
    // The dedicated server package requires a Vintagestory.net account login to download — WGS
    // cannot automate that, so this is a manual install: the user downloads it themselves once,
    // then points this server at that path.
    public override string Executable      => "VintagestoryServer.exe";
    public override int    DefaultPort     => 42420;
    public override int    DefaultQueryPort => 42420;
    public override int    DefaultMaxPlayers => 16;

    public override Task PreStartAsync(GameServer s)
    {
        WriteConfigIfMissing(Path.Combine(s.InstallPath, "serverconfig.json"), BuildConfig(s));
        return Task.CompletedTask;
    }

    private string BuildConfig(GameServer s)
    {
        var name = string.IsNullOrWhiteSpace(s.ServerName) ? "WGS Vintage Story Server" : s.ServerName;
        var pass = s.ServerPassword ?? "";
        return
            $$"""
            {
              "ServerName": "{{name}}",
              "Port": {{s.ServerPort}},
              "MaxClients": {{s.MaxPlayers}},
              "Password": "{{pass}}",
              "AdvertiseServer": true
            }
            """;
    }

    public override string BuildStartArguments(GameServer s) => "--dataPath \".\"";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
