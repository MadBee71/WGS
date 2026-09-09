using System.IO;
using System.Net.Http;
using System.Text.Json;
using WGS.Models;

namespace WGS.Games;

public class SpaceStation14Plugin : GamePluginBase
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WGS-WindowsGameServer/1.0");
        return http;
    }

    public override string GameId          => "ss14";
    public override string GameName        => "Space Station 14";
    public override string Description     => "⚠ Requires .NET 10 Runtime + latest VC++ Redistributable installed on the host BEFORE starting — multiplayer paranoia-and-chaos space station roleplay sim";
    public override string Category        => "Other";
    public override int    SteamAppId      => 0; // free, open source — the official build channel is off Steam
    public override string Executable      => "run_server.bat";
    public override string? GetWorkingDirectory(GameServer server) => server.InstallPath;
    public override int    DefaultPort     => 1212;
    public override int    DefaultQueryPort => 1212;
    public override int    DefaultMaxPlayers => 32;

    // Space Wizards Federation (the studio behind SS14) publishes ready-to-run builds directly via
    // Robust.Cdn — "wizards" is their own default content fork, distinct from the many community
    // forks that use the same CDN system under their own fork name. This is the vanilla/default
    // game. There is no JSON "latest" endpoint; the build listing page itself is scraped for the
    // newest win-x64 server link (verified: page lists builds newest-first, and the resulting
    // URL was confirmed to serve a real zip during testing).
    public override async Task<(string Build, string Url)?> GetManualDownloadInfoAsync(GameServer server)
    {
        try
        {
            var html = await _http.GetStringAsync("https://wizards.cdn.spacestation14.com/fork/wizards/");
            var match = System.Text.RegularExpressions.Regex.Match(html,
                "/fork/wizards/version/([0-9a-f]+)/file/SS14\\.Server_win-x64\\.zip");
            if (!match.Success) return null;
            var hash = match.Groups[1].Value;
            return (hash[..8], $"https://wizards.cdn.spacestation14.com/fork/wizards/version/{hash}/file/SS14.Server_win-x64.zip");
        }
        catch { return null; }
    }

    // Requires .NET 10 Runtime and the latest Microsoft Visual C++ Redistributable installed on
    // the host — neither can be silently installed by WGS without risking system-wide changes
    // the user didn't ask for.
    public override string BuildStartArguments(GameServer s) => string.Empty;

    public override Dictionary<string, string> GetDefaultSettings() => new();

    public override List<ConfigField> GetConfigFields() => BaseFields();
}
