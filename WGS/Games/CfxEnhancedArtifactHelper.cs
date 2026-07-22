using System.Net.Http;
using System.Text.Json;

namespace WGS.Games;

/// <summary>
/// Looks up cfx-server (FiveM for GTAV Enhanced) build download URLs from Cfx.re's changelog API.
/// Enhanced uses a separate artifact (cfx-server-win_x64.zip) from the legacy FXServer.exe.
/// </summary>
public static class CfxEnhancedArtifactHelper
{
    private static readonly HttpClient _http = new();

    // Separate changelog endpoint for enhanced/cfx-server builds.
    private const string EnhancedChangelogUrl = "https://changelogs-live.fivem.net/api/changelog/versions/win32/server_enhanced";

    // Fallback: same endpoint as legacy but may include enhanced fields in the future.
    private const string LegacyChangelogUrl = "https://changelogs-live.fivem.net/api/changelog/versions/win32/server";

    public record ArtifactInfo(string Build, string DownloadUrl);

    public static Task<ArtifactInfo?> GetLatestAsync()      => GetAsync("latest",      "latest_download");
    public static Task<ArtifactInfo?> GetRecommendedAsync() => GetAsync("recommended", "recommended_download");

    private static async Task<ArtifactInfo?> GetAsync(string buildField, string urlField)
    {
        // Try the enhanced-specific endpoint first; fall back to legacy endpoint.
        foreach (var url in new[] { EnhancedChangelogUrl, LegacyChangelogUrl })
        {
            try
            {
                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty(buildField, out var build) || !root.TryGetProperty(urlField, out var dlUrl))
                    continue;
                var buildStr = build.GetString();
                var urlStr   = dlUrl.GetString();
                if (buildStr != null && urlStr != null)
                    return new ArtifactInfo(buildStr, urlStr);
            }
            catch { }
        }
        return null;
    }
}
