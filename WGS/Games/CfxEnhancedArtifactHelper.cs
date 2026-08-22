namespace WGS.Games;

/// <summary>
/// Download helper for cfx-server (FiveM for GTAV Enhanced).
/// Cfx.re does not yet publish a versioned changelog API for Enhanced — a fixed
/// download URL is used until an official endpoint becomes available.
/// </summary>
public static class CfxEnhancedArtifactHelper
{
    private const string DownloadUrl = "https://downloads.cfx-services.net/prod/01a01f0e-7471-722b-a8ec-9a1827a4fdee/cfx-server_win_x64.zip";
    private const string BuildLabel  = "129";

    public record ArtifactInfo(string Build, string DownloadUrl);

    public static Task<ArtifactInfo?> GetLatestAsync()      => Task.FromResult<ArtifactInfo?>(new ArtifactInfo(BuildLabel, DownloadUrl));
    public static Task<ArtifactInfo?> GetRecommendedAsync() => Task.FromResult<ArtifactInfo?>(new ArtifactInfo(BuildLabel, DownloadUrl));
}
