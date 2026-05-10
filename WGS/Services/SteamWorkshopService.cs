using System.IO;
using System.Net.Http;
using System.Text.Json;
using WGS.Games;
using WGS.Models;

namespace WGS.Services;

public class WorkshopItem
{
    public ulong  PublishedFileId { get; set; }
    public string Title          { get; set; } = string.Empty;
    public string Description    { get; set; } = string.Empty;
    public bool   IsInstalled    { get; set; }
    public string InstallPath    { get; set; } = string.Empty;
}

public class SteamWorkshopService
{
    private readonly SteamCmdService _steamCmd;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public SteamWorkshopService(SteamCmdService steamCmd)
    {
        _steamCmd = steamCmd;
    }

    public bool SupportsWorkshop(IGamePlugin? plugin)
        => plugin != null && plugin.SteamAppId > 0 && plugin.WorkshopAppId > 0;

    public async Task<List<WorkshopItem>> GetInstalledItemsAsync(GameServer server, IGamePlugin plugin)
    {
        var result = new List<WorkshopItem>();
        var workshopDir = GetWorkshopPath(server, plugin);
        if (!Directory.Exists(workshopDir)) return result;

        foreach (var dir in Directory.GetDirectories(workshopDir))
        {
            var id = Path.GetFileName(dir);
            if (!ulong.TryParse(id, out var fileId)) continue;
            var item = new WorkshopItem
            {
                PublishedFileId = fileId,
                Title           = id, // will try to resolve
                IsInstalled     = true,
                InstallPath     = dir,
            };
            // Try to read item name from metadata file if present
            var meta = Path.Combine(dir, "wgs_workshop_meta.json");
            if (File.Exists(meta))
            {
                try
                {
                    var doc = JsonDocument.Parse(File.ReadAllText(meta));
                    if (doc.RootElement.TryGetProperty("title", out var t))
                        item.Title = t.GetString() ?? id;
                }
                catch { }
            }
            result.Add(item);
        }
        return await Task.FromResult(result);
    }

    public async Task InstallItemAsync(GameServer server, IGamePlugin plugin, ulong workshopItemId,
        IProgress<(int pct, string msg)>? progress = null)
    {
        if (!SupportsWorkshop(plugin))
            throw new InvalidOperationException("This game does not support Steam Workshop.");

        progress?.Report((0, $"Downloading Workshop item {workshopItemId}..."));

        // SteamCMD workshop_download_item command
        await _steamCmd.RunWorkshopDownloadAsync(plugin.SteamAppId, plugin.WorkshopAppId, workshopItemId,
            server.InstallPath, progress);

        // Try to fetch item title from Steam API for metadata
        var title = await TryGetItemTitleAsync(workshopItemId);
        if (title != null)
        {
            var workshopDir = Path.Combine(GetWorkshopPath(server, plugin), workshopItemId.ToString());
            Directory.CreateDirectory(workshopDir);
            File.WriteAllText(Path.Combine(workshopDir, "wgs_workshop_meta.json"),
                JsonSerializer.Serialize(new { title }));
        }

        progress?.Report((100, "Done."));
    }

    public void UninstallItem(GameServer server, IGamePlugin plugin, ulong workshopItemId)
    {
        var workshopDir = Path.Combine(GetWorkshopPath(server, plugin), workshopItemId.ToString());
        if (Directory.Exists(workshopDir))
            Directory.Delete(workshopDir, recursive: true);
    }

    private static string GetWorkshopPath(GameServer server, IGamePlugin plugin)
        => Path.Combine(server.InstallPath, "steamapps", "workshop", "content", plugin.WorkshopAppId.ToString());

    private static async Task<string?> TryGetItemTitleAsync(ulong id)
    {
        try
        {
            var url = $"https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
            var content = new FormUrlEncodedContent([
                new("itemcount", "1"),
                new("publishedfileids[0]", id.ToString()),
            ]);
            var resp = await _http.PostAsync(url, content);
            if (!resp.IsSuccessStatusCode) return null;
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement
                .GetProperty("response")
                .GetProperty("publishedfiledetails")[0]
                .GetProperty("title")
                .GetString();
        }
        catch { return null; }
    }
}
