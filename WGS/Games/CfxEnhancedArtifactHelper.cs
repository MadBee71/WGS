namespace WGS.Games;

/// <summary>
/// Download helper for cfx-server (FiveM for GTAV Enhanced).
/// Enhanced is in early access and has no versioned artifact API yet — Cfx.re
/// publishes a single "latest" ZIP at a fixed URL. Both Recommended and Latest
/// point to the same artifact until a proper changelog endpoint is available.
/// </summary>
public static class CfxEnhancedArtifactHelper
{
    // Fixed download URL published on https://docs.fivem.net/docs/server-download/
    // under "FiveM for GTAV Enhanced". No versioned API exists yet (early access).
    private const string DownloadUrl = "https://downloads.cfx-services.net/prod/019f8666-f8d0-7d16-9ea4-0ce263063f38/cfx-server_win_x64.zip";
    private const string BuildLabel  = "latest";

    public record ArtifactInfo(string Build, string DownloadUrl);

    public static Task<ArtifactInfo?> GetLatestAsync()      => Task.FromResult<ArtifactInfo?>(new ArtifactInfo(BuildLabel, DownloadUrl));
    public static Task<ArtifactInfo?> GetRecommendedAsync() => Task.FromResult<ArtifactInfo?>(new ArtifactInfo(BuildLabel, DownloadUrl));
}
