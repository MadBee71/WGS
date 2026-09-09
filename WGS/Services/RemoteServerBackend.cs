using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// IServerBackend implementation that proxies every call over HTTP to a WebApiService
/// instance — either this machine's own WGS.ServiceHost (localhost, "service mode") or a
/// genuinely remote machine. See Services/IServerBackend.cs for the contract and
/// Services/WebApiService.cs for the route surface this calls; modeled closely on
/// Services/RemoteMachineService.cs, which already does this same
/// HTTP-client-calling-WebApiService pattern for cross-machine control.
/// </summary>
public class RemoteServerBackend : IServerBackend
{
    private readonly HttpClient _http;
    private readonly string     _baseUrl;
    private readonly string     _token;

    public RemoteServerBackend(string baseUrl, string token)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _token   = token;
        _http    = new HttpClient { Timeout = TimeSpan.FromMinutes(10) }; // mod/workshop installs block the request
    }

    /// <summary>Allows an externally-owned HttpClient to be reused (e.g. shared with other
    /// remote services) instead of this class opening its own.</summary>
    public RemoteServerBackend(string baseUrl, string token, HttpClient sharedClient)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _token   = token;
        _http    = sharedClient;
    }

    // ── HTTP helpers ─────────────────────────────────────────────────────────

    private HttpRequestMessage Req(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, _baseUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return req;
    }

    private static StringContent JsonBody(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<JsonDocument> GetJsonAsync(string path)
    {
        using var req  = Req(HttpMethod.Get, path);
        var resp = await _http.SendAsync(req);
        var text  = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(ExtractError(text) ?? $"HTTP {(int)resp.StatusCode}");
        return JsonDocument.Parse(text.Length > 0 ? text : "{}");
    }

    private async Task<JsonDocument> PostJsonAsync(string path, object? body = null)
    {
        using var req = Req(HttpMethod.Post, path);
        req.Content = JsonBody(body ?? new { });
        var resp = await _http.SendAsync(req);
        var text  = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(ExtractError(text) ?? $"HTTP {(int)resp.StatusCode}");
        return JsonDocument.Parse(text.Length > 0 ? text : "{}");
    }

    /// <summary>Fire-and-forget style call (start/stop/restart/...) — throws on failure like the
    /// rest of this class rather than silently swallowing errors, since callers (ServerViewModel
    /// commands) already expect exceptions to surface to the UI the same way LocalServerBackend's
    /// direct service calls would.</summary>
    private Task PostAsync(string path, object? body = null) => PostJsonAsync(path, body);

    private static string? ExtractError(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        catch { return null; }
    }

    private static string Enc(string s) => Uri.EscapeDataString(s);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    // log is best-effort only — no progress channel over plain HTTP request/response, same as
    // InstallAsync below. The client's GetLogAsync polling picks up anything the service itself
    // writes to the server's console log (e.g. the pre-start/pre-stop backup messages).
    public Task StartAsync(GameServer server, IProgress<string>? log = null) => PostAsync($"/api/servers/{Enc(server.Id)}/start");
    public Task StopAsync(GameServer server, IProgress<string>? log = null)  => PostAsync($"/api/servers/{Enc(server.Id)}/stop");
    public Task KillAsync(GameServer server)    => PostAsync($"/api/servers/{Enc(server.Id)}/kill");
    public Task InstallAsync(GameServer server, IProgress<string>? log = null)
        // The HTTP call blocks until the remote install/update finishes (same simple pattern as
        // "backup" below), so there is no line-by-line progress to forward — best effort only.
        => PostAsync($"/api/servers/{Enc(server.Id)}/update");

    public async Task<bool> HasPendingBuildChannelChoiceAsync(string serverId)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(serverId)}/install/build-channel");
        return doc.RootElement.TryGetProperty("pending", out var p) && p.GetBoolean();
    }

    public async Task<(string recommended, string latest)?> GetBuildChannelOptionsAsync(string serverId)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(serverId)}/install/build-channel");
        if (!doc.RootElement.TryGetProperty("pending", out var p) || !p.GetBoolean()) return null;
        var recommended = doc.RootElement.TryGetProperty("recommended", out var r) ? r.GetString() ?? "" : "";
        var latest       = doc.RootElement.TryGetProperty("latest", out var l) ? l.GetString() ?? "" : "";
        return (recommended, latest);
    }

    public Task SubmitBuildChannelChoiceAsync(string serverId, bool useLatest)
        => PostAsync($"/api/servers/{Enc(serverId)}/install/build-channel", new { useLatest });

    // ── Console ──────────────────────────────────────────────────────────────

    public Task SendConsoleCommandAsync(string serverId, string command)
        => PostAsync($"/api/servers/{Enc(serverId)}/cmd", new { command });

    public async Task<(List<string> lines, List<string> types, int nextOffset)> GetLogAsync(string serverId, int offset)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(serverId)}/log?offset={offset}");
        var lines = doc.RootElement.GetProperty("lines").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        var types = doc.RootElement.GetProperty("types").EnumerateArray().Select(e => e.GetString() ?? "Info").ToList();
        var next  = doc.RootElement.GetProperty("nextOffset").GetInt32();
        return (lines, types, next);
    }

    // ── Backups ──────────────────────────────────────────────────────────────

    public async Task<BackupEntry> CreateBackupAsync(GameServer server)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/backups/create");
        return ParseBackupEntry(doc.RootElement);
    }

    public async Task<List<BackupEntry>> GetBackupsAsync(GameServer server)
    {
        // Uses the pre-existing GET /api/servers/{id}/backups route, which only carries
        // fileName/sizeText/createdAt — SizeBytes/IsIncremental/BaseFilePath aren't available
        // there, so this is a best-effort reconstruction. CreateBackupAsync/CleanupBackupsAsync
        // use the new /backups/create and /backups/cleanup routes instead, which return the
        // full BackupEntry shape.
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/backups");
        var list = new List<BackupEntry>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var fileName = el.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "" : "";
            var createdAt = el.TryGetProperty("createdAt", out var ca) && DateTime.TryParse(ca.GetString(), out var dt) ? dt : DateTime.MinValue;
            list.Add(new BackupEntry
            {
                FilePath    = fileName,
                ServerName  = server.DisplayName,
                CreatedAt   = createdAt,
                SizeBytes   = 0,
                IsIncremental = false,
                BaseFilePath  = "",
            });
        }
        return list;
    }

    public async Task RestoreBackupAsync(GameServer server, string fileName)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/restore", new { fileName });
    }

    public async Task DeleteBackupAsync(GameServer server, string fileName)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/backups/delete", new { fileName });
    }

    public async Task<List<BackupEntry>> CleanupBackupsAsync(GameServer server)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/backups/cleanup");
        return doc.RootElement.EnumerateArray().Select(ParseBackupEntry).ToList();
    }

    private static BackupEntry ParseBackupEntry(JsonElement el) => new()
    {
        FilePath      = el.TryGetProperty("FilePath", out var fp) ? fp.GetString() ?? "" : "",
        ServerName    = el.TryGetProperty("ServerName", out var sn) ? sn.GetString() ?? "" : "",
        CreatedAt     = el.TryGetProperty("createdAt", out var ca) && DateTime.TryParse(ca.GetString(), out var dt) ? dt : DateTime.MinValue,
        SizeBytes     = el.TryGetProperty("SizeBytes", out var sb) ? sb.GetInt64() : 0,
        IsIncremental = el.TryGetProperty("IsIncremental", out var inc) && inc.GetBoolean(),
        BaseFilePath  = el.TryGetProperty("BaseFilePath", out var bf) ? bf.GetString() ?? "" : "",
    };

    // ── Config file editor ───────────────────────────────────────────────────

    public async Task<List<ConfigFileEntry>> GetConfigFilesAsync(GameServer server)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/config-files");
        return doc.RootElement.EnumerateArray().Select(el => new ConfigFileEntry
        {
            Name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
            Path = el.TryGetProperty("Path", out var p) ? p.GetString() ?? "" : "",
        }).ToList();
    }

    public async Task<string> ReadConfigFileAsync(GameServer server, string fileName)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/config-files/content?path={Enc(fileName)}");
        return doc.RootElement.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
    }

    public async Task SaveConfigFileAsync(GameServer server, string fileName, string content)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/config-files/save",
            new { path = fileName, content });
    }

    public async Task<List<ConfigSnapshot>> GetConfigSnapshotsAsync(GameServer server, string fileName)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/config-files/snapshots?path={Enc(fileName)}");
        return doc.RootElement.EnumerateArray().Select(el => new ConfigSnapshot
        {
            FilePath = el.TryGetProperty("FilePath", out var fp) ? fp.GetString() ?? "" : "",
            SavedAt  = el.TryGetProperty("savedAt", out var sa) && DateTime.TryParse(sa.GetString(), out var dt) ? dt : DateTime.MinValue,
        }).ToList();
    }

    public async Task RestoreConfigSnapshotAsync(GameServer server, ConfigSnapshot snapshot)
    {
        // ConfigSnapshot only carries the .bak file's own path (not which live config file it
        // belongs to) — see WebApiService.RestoreConfigSnapshot doc comment. The service side
        // resolves the target file from the snapshot's directory naming convention once that
        // wiring lands (later integration step); this call just forwards the snapshot path.
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/config-files/restore-snapshot",
            new { snapshotPath = snapshot.FilePath });
    }

    // ── Config presets ───────────────────────────────────────────────────────

    public async Task<string?> ApplyPresetAsync(GameServer server, ConfigPreset preset)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/presets/apply",
            new { configFile = preset.ConfigFile, name = preset.Name, values = preset.Values });
        return doc.RootElement.TryGetProperty("backupPath", out var bp) && bp.ValueKind != JsonValueKind.Null
            ? bp.GetString() : null;
    }

    // ── Mods ─────────────────────────────────────────────────────────────────

    public async Task InstallModFrameworkAsync(GameServer server, string framework, IProgress<(int pct, string msg)> progress)
    {
        // Blocks until the remote install finishes — no progress channel over plain HTTP
        // request/response, matching the "backup"/"update" actions' existing behavior.
        progress?.Report((0, $"Installing {framework} on remote server..."));
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/mods/{Enc(framework)}/install");
        progress?.Report((100, "Done"));
    }

    // ── SourceMod ────────────────────────────────────────────────────────────

    public async Task<(List<SourceModPlugin> active, List<SourceModPlugin> disabled)> GetSourceModPluginsAsync(GameServer server)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/sourcemod/plugins");
        List<SourceModPlugin> ParseList(string prop) =>
            doc.RootElement.TryGetProperty(prop, out var arr)
                ? arr.EnumerateArray().Select(el => new SourceModPlugin(
                    el.TryGetProperty("FileName", out var fn) ? fn.GetString() ?? "" : "",
                    el.TryGetProperty("IsDisabled", out var d) && d.GetBoolean())).ToList()
                : [];
        return (ParseList("active"), ParseList("disabled"));
    }

    public async Task SetSourceModPluginEnabledAsync(GameServer server, string fileName, bool enabled)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/sourcemod/toggle",
            new { fileName, enabled });
    }

    // ── Steam Workshop ───────────────────────────────────────────────────────

    public async Task<List<WorkshopMod>> GetWorkshopModsAsync(GameServer server)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/workshop/mods");
        return doc.RootElement.EnumerateArray().Select(ParseWorkshopMod).ToList();
    }

    public async Task<List<WorkshopItem>> SearchWorkshopAsync(GameServer server, string query)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/workshop/search?q={Enc(query)}");
        return doc.RootElement.EnumerateArray().Select(el => new WorkshopItem
        {
            PublishedFileId = el.TryGetProperty("PublishedFileId", out var id) ? id.GetUInt64() : 0,
            Title           = el.TryGetProperty("Title", out var t) ? t.GetString() ?? "" : "",
            Description     = el.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "",
            PreviewUrl      = el.TryGetProperty("PreviewUrl", out var p) ? p.GetString() ?? "" : "",
            IsInstalled     = el.TryGetProperty("IsInstalled", out var ii) && ii.GetBoolean(),
            InstallPath     = el.TryGetProperty("InstallPath", out var ip) ? ip.GetString() ?? "" : "",
        }).ToList();
    }

    public async Task InstallWorkshopItemAsync(GameServer server, ulong itemId, IProgress<(int pct, string msg)>? progress = null)
    {
        // Best-effort only — no progress channel over plain HTTP request/response, same as InstallAsync.
        progress?.Report((0, "Installing workshop item on remote server..."));
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/workshop/install", new { itemId });
        progress?.Report((100, "Done"));
    }

    public async Task UninstallWorkshopItemAsync(GameServer server, WorkshopMod mod)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/workshop/uninstall", new { modId = mod.ModId });
    }

    public async Task SetWorkshopModEnabledAsync(GameServer server, WorkshopMod mod, bool enabled)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/workshop/toggle",
            new { modId = mod.ModId, enabled });
    }

    public async Task<List<WorkshopMod>> CheckOutdatedModsAsync(GameServer server)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/workshop/outdated");
        return doc.RootElement.EnumerateArray().Select(ParseWorkshopMod).ToList();
    }

    public async Task UpdateModsAsync(GameServer server, IEnumerable<WorkshopMod>? onlyThese = null, IProgress<(int pct, string msg)>? progress = null)
    {
        progress?.Report((0, "Updating mods on remote server..."));
        var modIds = onlyThese?.Select(m => m.ModId).ToList();
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/workshop/update", new { modIds });
        progress?.Report((100, "Done"));
    }

    private static WorkshopMod ParseWorkshopMod(JsonElement el) => new()
    {
        ServerId    = el.TryGetProperty("ServerId", out var sid) ? sid.GetString() ?? "" : "",
        ModId       = el.TryGetProperty("ModId", out var mid) ? mid.GetUInt64() : 0,
        ModName     = el.TryGetProperty("ModName", out var mn) ? mn.GetString() ?? "" : "",
        IsEnabled   = el.TryGetProperty("IsEnabled", out var ie) ? ie.GetBoolean() : true,
        LastUpdated = el.TryGetProperty("LastUpdated", out var lu) && DateTime.TryParse(lu.GetString(), out var dt) ? dt : DateTime.MinValue,
    };

    // ── Cleanup ──────────────────────────────────────────────────────────────

    public async Task<List<string>> CleanupJunkFilesAsync(GameServer server)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/cleanup-junk");
        return doc.RootElement.TryGetProperty("removed", out var r)
            ? r.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
            : [];
    }

    // ── Templates (global, per-GameId — see WebApiService.cs and TemplateService.cs;
    // SaveAsTemplateAsync only reads `server` to seed the new template) ─────────────

    public async Task SaveAsTemplateAsync(GameServer server, string name, string description, string category, List<string> tags)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/templates/save",
            new { name, description, category, tags });
    }

    public async Task ApplyTemplateAsync(GameServer server, ServerTemplate template)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/templates/apply",
            new { templateId = template.Id });
    }

    public async Task<ServerTemplate> CloneTemplateAsync(ServerTemplate template)
    {
        using var doc = await PostJsonAsync($"/api/templates/{Enc(template.Id)}/clone");
        return JsonSerializer.Deserialize<ServerTemplate>(doc.RootElement.GetRawText())
            ?? throw new InvalidOperationException("Clone response could not be parsed");
    }

    public async Task DeleteTemplateAsync(ServerTemplate template)
    {
        using var doc = await PostJsonAsync($"/api/templates/{Enc(template.Id)}/delete");
    }

    // ── Scheduled tasks ──────────────────────────────────────────────────────

    public async Task<List<ScheduledTask>> GetScheduledTasksAsync(string serverId)
    {
        using var doc = await GetJsonAsync("/api/scheduled-tasks");
        var all = JsonSerializer.Deserialize<List<ScheduledTaskDto>>(doc.RootElement.GetRawText()) ?? [];
        // The existing GET /api/scheduled-tasks route returns display-formatted fields (action
        // text, frequency text) rather than the raw ScheduledTask shape — reconstructing a full
        // ScheduledTask isn't possible from that alone (Action/Frequency enums, TimeOfDay,
        // DayOfWeek, IntervalMinutes, Command aren't in the response). Filtering by serverId is
        // still meaningful for a caller that only needs to know what's scheduled, so this returns
        // best-effort ScheduledTask objects with Id/ServerId/ServerName/IsEnabled/LastRun/NextRun
        // populated and the rest left at defaults.
        return all.Where(t => t.ServerId == serverId).Select(t => new ScheduledTask
        {
            Id         = t.Id,
            ServerId   = t.ServerId,
            ServerName = t.ServerName,
            IsEnabled  = t.IsEnabled,
        }).ToList();
    }

    private record ScheduledTaskDto(string Id, string ServerId, string ServerName, bool IsEnabled);

    public async Task AddScheduledTaskAsync(ScheduledTask task)
    {
        using var doc = await PostJsonAsync("/api/scheduled-tasks", task);
    }

    public async Task RemoveScheduledTaskAsync(ScheduledTask task)
    {
        using var doc = await PostJsonAsync($"/api/scheduled-tasks/{Enc(task.Id)}/delete");
    }

    // ── Quick commands / log-watch rules ─────────────────────────────────────

    public async Task AddQuickCommandAsync(GameServer server, QuickCommand qc)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/quick-commands", qc);
    }

    public async Task RemoveQuickCommandAsync(GameServer server, QuickCommand qc)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/quick-commands/remove", qc);
    }

    public async Task AddLogWatchRuleAsync(GameServer server, Models.LogWatchRule rule)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/log-watch-rules", rule);
    }

    public async Task RemoveLogWatchRuleAsync(GameServer server, Models.LogWatchRule rule)
    {
        using var doc = await PostJsonAsync($"/api/servers/{Enc(server.Id)}/log-watch-rules/remove", rule);
    }

    // ── Players ──────────────────────────────────────────────────────────────

    public async Task<List<Models.OnlinePlayer>> GetOnlinePlayersAsync(GameServer server)
    {
        using var doc = await GetJsonAsync($"/api/servers/{Enc(server.Id)}/players");
        return doc.RootElement.EnumerateArray().Select(el => new Models.OnlinePlayer
        {
            Name             = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
            SteamId          = el.TryGetProperty("SteamId", out var sid) ? sid.GetString() ?? "" : "",
            Ping             = el.TryGetProperty("Ping", out var p) ? p.GetInt32() : 0,
            ConnectedSeconds = el.TryGetProperty("ConnectedSeconds", out var cs) ? cs.GetInt32() : 0,
        }).ToList();
    }

    public void Dispose() => _http.Dispose();
}
