using System.Net.Http;
using System.Text.Json;

namespace WGS.Games;

/// <summary>
/// Looks up the latest FXServer build download URL from Cfx.re's own changelog API.
/// Both FiveM and RedM run the exact same fxserver.exe binary (confirmed: same artifact,
/// same Cfx.re base server, only the game-specific data/config differs) — same lookup for both.
/// </summary>
public static class CfxArtifactHelper
{
    private static readonly HttpClient _http = new();
    private const string ChangelogUrl = "https://changelogs-live.fivem.net/api/changelog/versions/win32/server";

    public static async Task<string?> GetLatestServerDownloadUrlAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(ChangelogUrl);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("latest_download", out var url) ? url.GetString() : null;
        }
        catch { return null; }
    }
}
