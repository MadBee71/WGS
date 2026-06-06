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
    public string PreviewUrl     { get; set; } = string.Empty;
    public bool   IsInstalled    { get; set; }
    public string InstallPath    { get; set; } = string.Empty;
}

public class SteamWorkshopService
{
    private readonly SteamCmdService  _steamCmd;
    private readonly WorkshopDbService _db;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public SteamWorkshopService(SteamCmdService steamCmd, WorkshopDbService db)
    {
        _steamCmd = steamCmd;
        _db       = db;
    }

    public bool SupportsWorkshop(IGamePlugin? plugin)
        => plugin != null && plugin.SteamAppId > 0 && plugin.WorkshopAppId > 0;

    // ── Installed items (from DB + filesystem) ────────────────────────────────

    public List<WorkshopMod> GetServerMods(string serverId)
        => _db.GetModsForServer(serverId);

    public Task<List<WorkshopItem>> GetInstalledItemsAsync(GameServer _, IGamePlugin plugin)
    {
        var result = new List<WorkshopItem>();
        var workshopDir = GetWorkshopPath(plugin);
        if (!Directory.Exists(workshopDir)) return Task.FromResult(result);

        foreach (var dir in Directory.GetDirectories(workshopDir))
        {
            var id = Path.GetFileName(dir);
            if (!ulong.TryParse(id, out var fileId)) continue;
            var item = new WorkshopItem
            {
                PublishedFileId = fileId,
                Title           = id,
                IsInstalled     = true,
                InstallPath     = dir,
            };
            var meta = Path.Combine(dir, "wgs_workshop_meta.json");
            if (File.Exists(meta))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(meta));
                    if (doc.RootElement.TryGetProperty("title", out var t))
                        item.Title = t.GetString() ?? id;
                    if (doc.RootElement.TryGetProperty("preview_url", out var p))
                        item.PreviewUrl = p.GetString() ?? string.Empty;
                }
                catch { }
            }
            result.Add(item);
        }
        return Task.FromResult(result);
    }

    // ── Install ───────────────────────────────────────────────────────────────

    public async Task InstallItemAsync(GameServer server, IGamePlugin plugin, ulong workshopItemId,
        IProgress<(int pct, string msg)>? progress = null)
    {
        if (!SupportsWorkshop(plugin))
            throw new InvalidOperationException("This game does not support Steam Workshop.");

        progress?.Report((0, $"Downloading Workshop item {workshopItemId}..."));

        await _steamCmd.RunWorkshopDownloadAsync(plugin.SteamAppId, plugin.WorkshopAppId, workshopItemId,
            server.InstallPath, progress);

        var itemPath = GetWorkshopItemPath(plugin, workshopItemId);

        // Fetch title + preview from Steam API
        var (title, previewUrl) = await TryGetItemDetailsAsync(workshopItemId);
        title ??= workshopItemId.ToString();

        // Save metadata for offline use
        if (Directory.Exists(itemPath))
        {
            try { File.WriteAllText(Path.Combine(itemPath, "wgs_workshop_meta.json"),
                JsonSerializer.Serialize(new { title, preview_url = previewUrl ?? "" })); }
            catch { }
        }

        // Call plugin hook (symlinks, key copies, config edits)
        if (plugin is IWorkshopPlugin wp && Directory.Exists(itemPath))
        {
            try { await wp.OnModDownloadedAsync(server.InstallPath, itemPath, workshopItemId, title); }
            catch (Exception ex)
            {
                progress?.Report((95, $"⚠ Post-install hook failed: {ex.Message}"));
            }
        }

        // Persist to DB
        _db.UpsertMod(new WorkshopMod
        {
            ServerId    = server.Id,
            ModId       = workshopItemId,
            ModName     = title,
            IsEnabled   = true,
            LastUpdated = DateTime.Now,
        });

        progress?.Report((100, "Done."));
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    public async Task UninstallItemAsync(GameServer server, IGamePlugin plugin, ulong workshopItemId)
    {
        var itemPath = GetWorkshopItemPath(plugin, workshopItemId);
        var mod      = _db.GetModsForServer(server.Id).FirstOrDefault(m => m.ModId == workshopItemId);
        var modName  = mod?.ModName ?? workshopItemId.ToString();

        if (plugin is IWorkshopPlugin wp)
        {
            try { await wp.OnModRemovedAsync(server.InstallPath, itemPath, workshopItemId, modName); }
            catch { }
        }

        if (Directory.Exists(itemPath))
        {
            try { Directory.Delete(itemPath, recursive: true); }
            catch { }
        }

        _db.DeleteMod(server.Id, workshopItemId);
    }


    // ── Update all ────────────────────────────────────────────────────────────

    public async Task UpdateAllModsAsync(GameServer server, IGamePlugin plugin,
        IProgress<(int pct, string msg)>? progress = null)
    {
        if (!SupportsWorkshop(plugin)) return;

        var mods = _db.GetModsForServer(server.Id);
        if (mods.Count == 0) return;

        for (int i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];
            int basePct = (int)((double)i / mods.Count * 100);
            progress?.Report((basePct, $"Updating {mod.ModName} ({i + 1}/{mods.Count})..."));

            try
            {
                var itemProgress = new Progress<(int pct, string msg)>(x =>
                    progress?.Report((basePct + x.pct / mods.Count, x.msg)));
                await InstallItemAsync(server, plugin, mod.ModId, itemProgress);
            }
            catch (Exception ex)
            {
                progress?.Report((basePct, $"⚠ {mod.ModName}: {ex.Message}"));
            }
        }

        progress?.Report((100, "All mods updated."));
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public async Task<List<WorkshopItem>> SearchWorkshopAsync(IGamePlugin plugin, string query,
        string? steamApiKey = null, int count = 10)
    {
        if (plugin.WorkshopAppId <= 0) return [];

        try
        {
            var key    = steamApiKey ?? string.Empty;
            var url    = $"https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/" +
                         $"?key={key}&appid={plugin.WorkshopAppId}&search_text={Uri.EscapeDataString(query)}" +
                         $"&numperpage={count}&return_short_description=true&return_previews=true";

            var resp = await _http.GetStringAsync(url);
            var doc  = JsonDocument.Parse(resp);

            var items = new List<WorkshopItem>();
            if (!doc.RootElement.TryGetProperty("response", out var res)) return items;
            if (!res.TryGetProperty("publishedfiledetails", out var files)) return items;

            foreach (var f in files.EnumerateArray())
            {
                if (!f.TryGetProperty("publishedfileid", out var idEl)) continue;
                var idStr = idEl.GetString() ?? "0";
                if (!ulong.TryParse(idStr, out var id)) continue;

                items.Add(new WorkshopItem
                {
                    PublishedFileId = id,
                    Title           = f.TryGetProperty("title",             out var t)  ? (t.GetString()  ?? idStr) : idStr,
                    Description     = f.TryGetProperty("short_description", out var d)  ? (d.GetString()  ?? "")    : "",
                    PreviewUrl      = f.TryGetProperty("preview_url",       out var p)  ? (p.GetString()  ?? "")    : "",
                });
            }
            return items;
        }
        catch { return []; }
    }

    // ── Mod argument builder ──────────────────────────────────────────────────

    /// <summary>Returns the mod startup argument for enabled mods, or empty string.</summary>
    public string BuildModArguments(GameServer server, IGamePlugin plugin)
    {
        if (plugin is not IWorkshopPlugin wp) return string.Empty;
        var enabledIds = _db.GetModsForServer(server.Id)
            .Where(m => m.IsEnabled)
            .Select(m => m.ModId)
            .ToList();
        return enabledIds.Count > 0 ? wp.BuildModArguments(enabledIds, server.InstallPath) : string.Empty;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetWorkshopPath(IGamePlugin plugin)
        => Path.Combine(_steamCmd.WorkshopContentPath, plugin.WorkshopAppId.ToString());

    private string GetWorkshopItemPath(IGamePlugin plugin, ulong itemId)
        => Path.Combine(GetWorkshopPath(plugin), itemId.ToString());

    public static async Task<string?> TryGetItemTitleAsync(ulong id)
        => (await TryGetItemDetailsAsync(id)).title;

    public static async Task<(string? title, string? previewUrl)> TryGetItemDetailsAsync(ulong id)
    {
        try
        {
            var url     = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
            var content = new FormUrlEncodedContent([
                new("itemcount", "1"),
                new("publishedfileids[0]", id.ToString()),
            ]);
            var resp = await _http.PostAsync(url, content);
            if (!resp.IsSuccessStatusCode) return (null, null);
            var doc    = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var detail = doc.RootElement.GetProperty("response").GetProperty("publishedfiledetails")[0];
            var title  = detail.TryGetProperty("title",       out var t) ? t.GetString() : null;
            var prev   = detail.TryGetProperty("preview_url", out var p) ? p.GetString() : null;
            return (title, prev);
        }
        catch { return (null, null); }
    }

    // ── Symlink helper (used by plugins) ──────────────────────────────────────

    /// <summary>Creates a directory symlink or junction. Requires admin rights on Windows.</summary>
    public static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        if (Directory.Exists(linkPath))
        {
            // Remove existing symlink/junction but not a real directory
            try
            {
                var info = new System.IO.DirectoryInfo(linkPath);
                if ((info.Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
                    Directory.Delete(linkPath);
                else
                    return; // real directory — leave it alone
            }
            catch { }
        }

        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (UnauthorizedAccessException)
        {
            // Fallback: directory junction via mklink /j (works without admin on Windows)
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName  = "cmd.exe",
                Arguments = $"/c mklink /j \"{linkPath}\" \"{targetPath}\"",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5000);
        }
    }

    /// <summary>Removes a directory symlink/junction (not a real directory).</summary>
    public static void RemoveDirectoryLink(string linkPath)
    {
        try
        {
            if (!Directory.Exists(linkPath)) return;
            var info = new System.IO.DirectoryInfo(linkPath);
            if ((info.Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
                Directory.Delete(linkPath);
        }
        catch { }
    }
}
