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
    /// Returns (hasUpdate, latestTag, zipDownloadUrl) or (false, "", "") on any error.
    /// Never throws.
    /// </summary>
    public static async Task<(bool HasUpdate, string LatestVersion, string DownloadUrl)> CheckAsync()
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
                return (false, "", "");

            var tag = tagEl.GetString() ?? "";
            var latestStr = tag.TrimStart('v');

            if (!Version.TryParse(latestStr, out var latest))
                return (false, tag, "");

            if (!Version.TryParse(GetCurrentVersion(), out var current))
                return (false, tag, "");

            if (latest <= current)
                return (false, tag, "");

            // Find the zip asset URL
            var downloadUrl = string.Empty;
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }
            }

            return (true, tag, downloadUrl);
        }
        catch
        {
            return (false, "", "");
        }
    }

    public static string GetCurrentVersion() =>
        Assembly.GetExecutingAssembly()
                .GetName().Version?
                .ToString(3) ?? "1.1.0";
}
