using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace WGS.Services;

/// <summary>
/// Checks GitHub releases for a newer version of WGS.
/// Fully self-contained — no DI registration required.
/// </summary>
public static class UpdateCheckerService
{
    private const string ApiUrl = "https://api.github.com/repos/MadBee71/WGS/releases/latest";
    public  const string ReleasesUrl = "https://github.com/MadBee71/WGS/releases/latest";

    /// <summary>
    /// Returns (hasUpdate, latestTag) or (false, "") on any error.
    /// Never throws.
    /// </summary>
    public static async Task<(bool HasUpdate, string LatestVersion)> CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("WGS", GetCurrentVersion()));

            var json = await http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tag_name", out var tagEl))
                return (false, "");

            var tag = tagEl.GetString() ?? "";
            var latestStr = tag.TrimStart('v');

            if (!Version.TryParse(latestStr, out var latest))
                return (false, tag);

            if (!Version.TryParse(GetCurrentVersion(), out var current))
                return (false, tag);

            return (latest > current, tag);
        }
        catch
        {
            return (false, "");
        }
    }

    public static string GetCurrentVersion() =>
        Assembly.GetExecutingAssembly()
                .GetName().Version?
                .ToString(3) ?? "1.1.0";
}
