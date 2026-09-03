namespace WGS.Games;

/// <summary>
/// Download helper for cfx-server (FiveM for GTAV Enhanced).
/// Cfx.re does not yet publish a versioned changelog API for Enhanced — a fixed
/// download URL is used until an official endpoint becomes available.
/// </summary>
public static class CfxEnhancedArtifactHelper
{
    private const string DownloadUrl = "https://downloads.cfx-services.net/prod/01a05860-2cbf-73e1-96f0-cec4a003f38c/cfx-server_win_x64.zip";
    private const string BuildLabel  = "139";

    public record ArtifactInfo(string Build, string DownloadUrl);

    public static Task<ArtifactInfo?> GetLatestAsync()      => Task.FromResult<ArtifactInfo?>(new ArtifactInfo(BuildLabel, DownloadUrl));
    public static Task<ArtifactInfo?> GetRecommendedAsync() => Task.FromResult<ArtifactInfo?>(new ArtifactInfo(BuildLabel, DownloadUrl));
}
