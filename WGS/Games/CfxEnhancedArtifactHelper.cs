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
    private const string ChangelogUrl = "https://raw.githubusercontent.com/SkOODaT/CfxVersions/refs/heads/main/versions.json";

    public record ArtifactInfo(string Build, string DownloadUrl);

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
            var urlStr = url.GetString();
            return buildStr != null && urlStr != null ? new ArtifactInfo(buildStr, urlStr) : null;
        }
        catch { return null; }
    }
}
