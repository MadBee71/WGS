using System.IO;
using WGS.Models;

namespace WGS.Games;

public class CryoFallPlugin : GamePluginBase
{
    public override string GameId          => "cryofall";
    public override string GameName        => "CryoFall";
    public override string Description     => "Top-down sci-fi survival game — runs on .NET, not SteamCMD-installable";
    public override string Category        => "Survival";
    public override int    SteamAppId      => 0; // confirmed: cannot be installed/updated via SteamCMD
    public override string Executable      => "dotnet";
    public override string? GetWorkingDirectory(GameServer server) => Path.Combine(server.InstallPath, "Binaries", "Server");
    public override int    DefaultPort     => 6000;
    public override int    DefaultQueryPort => 6000;
    public override int    DefaultMaxPlayers => 16;

    // Verified working as of this writing (real 478MB payload, HTTP 200) — CryoFall ships
    // infrequent updates, so this version-pinned URL may need a manual bump eventually; there is
    // no version-agnostic "latest" endpoint published.
    public override Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
        => Task.FromResult<(string, string)?>(("1.33.1.15", "https://atomictorch.com/Files/CryoFall_Server_v1.33.1.15.zip"));

    public override string BuildStartArguments(GameServer s) => "CryoFall_Server.dll loadOrNew";

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
