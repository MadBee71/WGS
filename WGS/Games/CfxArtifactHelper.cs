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

    public record ArtifactInfo(string Build, string DownloadUrl);

    public static async Task<string?> GetLatestServerDownloadUrlAsync()
        => (await GetLatestAsync())?.DownloadUrl;

    /// <summary>The newest build — gets new features fastest, but per Cfx.re's own guidance can be buggy.</summary>
    public static Task<ArtifactInfo?> GetLatestAsync() => GetAsync("latest", "latest_download");

    /// <summary>The build Cfx.re currently recommends for production use — slower to get new features, more stable.</summary>
    public static Task<ArtifactInfo?> GetRecommendedAsync() => GetAsync("recommended", "recommended_download");

    private static async Task<ArtifactInfo?> GetAsync(string buildField, string urlField)
    {
        try
        {
            var json = await _http.GetStringAsync(ChangelogUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty(buildField, out var build) || !root.TryGetProperty(urlField, out var url))
                return null;
            var buildStr = build.GetString();
            var urlStr   = url.GetString();
            return buildStr != null && urlStr != null ? new ArtifactInfo(buildStr, urlStr) : null;
        }
        catch { return null; }
    }
}
