using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using WGS.Models;

namespace WGS.Services;

/// <summary>
/// Lightweight embedded HTTP server exposing a REST API + single-page UI.
/// Uses HttpListener (no ASP.NET Core dependency).
/// </summary>
public class WebApiService : IDisposable
{
    private HttpListener?  _listener;
    private CancellationTokenSource? _cts;
    private Task?          _serverTask;
    private readonly Dictionary<string, DateTime> _lastPublicWake = new();
    private static readonly TimeSpan PublicWakeCooldown = TimeSpan.FromSeconds(60);

    public bool   IsRunning   { get; private set; }
    public int    Port        { get; private set; } = 8765;
    // Fallback single-token (käytetään jos UserService ei ole saatavilla)
    public string Token       { get; private set; } = Guid.NewGuid().ToString("N");

    // Injektoidaan käyttäjähallintaa varten
    public UserService? Users { get; set; }

    // Callbacks wired by MainViewModel
    public Func<IEnumerable<GameServer>>?                   GetServers    { get; set; }
    public Func<string, Task>?                              StartServer   { get; set; }
    public Func<string, Task>?                              StopServer    { get; set; }
    public Func<string, Task>?                              RestartServer { get; set; }
    public Func<string, Task>?                              UpdateServer  { get; set; }
    public Func<string, Task>?                              BackupServer  { get; set; }
    public Func<string, string, Task>?                      SendCmd       { get; set; }
    public Func<SystemMetrics>?                             GetMetrics    { get; set; }
    public Func<(double inKbs, double outKbs)>?             GetNetwork    { get; set; }
    /// <summary>Returns log lines for a server starting at the given offset index.</summary>
    public Func<string, int, (List<string> lines, List<string> types, int nextOffset)>? GetLog { get; set; }
    /// <summary>Returns uptime string for a running server, or null.</summary>
    public Func<string, string?>? GetUptime { get; set; }
    /// <summary>Returns online player list for a server (name, steamId, ping, connectedSeconds).</summary>
    public Func<string, IEnumerable<Models.OnlinePlayer>>? GetOnlinePlayers { get; set; }
    /// <summary>Returns list of backup entries for a server.</summary>
    public Func<string, IEnumerable<(string fileName, string sizeText, DateTime createdAt)>>? GetBackups { get; set; }
    /// <summary>Restores a backup by filename for a server. Returns error message or null on success.</summary>
    public Func<string, string, Task<string?>>? RestoreBackup { get; set; }
    public Func<string, int, IEnumerable<(DateTime time, double cpu, long memMb)>>? GetPerfSamples { get; set; }
    /// <summary>Save a note for a server. Returns error or null on success.</summary>
    public Func<string, string, Task<string?>>? SaveNote { get; set; }
    /// <summary>Returns all scheduled tasks.</summary>
    public Func<IEnumerable<ScheduledTask>>? GetScheduledTasks { get; set; }
    /// <summary>Manually trigger a scheduled task by id. Returns error or null on success.</summary>
    public Func<string, Task<string?>>? RunScheduledTask { get; set; }
    /// <summary>Returns all log lines for a server (for download).</summary>
    public Func<string, IEnumerable<string>>? GetFullLog { get; set; }

    /// <summary>True when listener bound to all interfaces (reachable from network); false = localhost only.</summary>
    public bool BoundToAllInterfaces { get; private set; }

    /// <summary>True when the web dashboard and full REST API are enabled. False = slave-only mode (master/slave comms still work).</summary>
    public bool DashboardEnabled { get; set; } = false;

    public void Start(int port, string token)
    {
        Stop();
        Port  = port;
        Token = token;

        // Ensure Windows Firewall allows inbound traffic on this port
        AddFirewallRule(port);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{port}/");
        try
        {
            _listener.Start();
            BoundToAllInterfaces = true;
        }
        catch (HttpListenerException)
        {
            // Try to register the URL ACL so non-admin processes can bind to all interfaces
            RegisterUrlAcl(port);

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{port}/");
            try
            {
                _listener.Start();
                BoundToAllInterfaces = true;
            }
            catch
            {
                // Still failing — fall back to localhost only
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                BoundToAllInterfaces = false;
            }
        }

        _cts        = new CancellationTokenSource();
        _serverTask = Task.Run(() => ListenLoop(_cts.Token));
        IsRunning   = true;
    }

    private static void AddFirewallRule(int port)
    {
        try
        {
            var name = $"WGS Web API port {port}";
            // Remove old rule first (ignore errors), then add fresh
            RunNetsh($"advfirewall firewall delete rule name=\"{name}\"");
            RunNetsh($"advfirewall firewall add rule name=\"{name}\" dir=in action=allow protocol=TCP localport={port}");
        }
        catch { }
    }

    private static void RegisterUrlAcl(int port)
    {
        // "Everyone" is localized on non-English Windows — try English first,
        // then the built-in SID-backed alias that netsh accepts on all locales.
        foreach (var user in new[] { "Everyone", "\\Everyone", "NT AUTHORITY\\Authenticated Users" })
        {
            try
            {
                RunNetsh($"http add urlacl url=http://+:{port}/ user=\"{user}\"");
                return; // stop on first success
            }
            catch { }
        }
    }

    private static void RunNetsh(string args)
    {
        using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("netsh", args)
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
        });
        proc?.WaitForExit(4000);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
        IsRunning = false;
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener!.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => HandleRequest(ctx), ct)
                    .ContinueWith(t => Console.WriteLine($"[WGS] API request error: {t.Exception?.InnerException?.Message}"),
                        TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        var req  = ctx.Request;
        var resp = ctx.Response;
        resp.Headers["Access-Control-Allow-Origin"]  = "*";
        resp.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
        resp.Headers["Access-Control-Allow-Headers"] = "Authorization,Content-Type";

        if (req.HttpMethod == "OPTIONS") { resp.StatusCode = 204; resp.Close(); return; }

        var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "/";

        if (req.HttpMethod == "GET" && path == "/favicon.ico")
        {
            await SendLogoAsync(resp);
            return;
        }

        // Serve UI page — only when dashboard is explicitly enabled
        if (path == "" || path == "/" || path == "/ui")
        {
            if (!DashboardEnabled)
            {
                resp.StatusCode = 403;
                await SendHtml(resp, "<html><body style='font-family:sans-serif;background:#0d1117;color:#8b949e;display:flex;align-items:center;justify-content:center;height:100vh;margin:0'><p>Web dashboard is disabled. Enable it in WGS Settings → Web Remote Control.</p></body></html>");
                return;
            }
            await SendHtml(resp, BuildUiHtml());
            return;
        }

        // Public, read-only, shareable status page — no auth token needed (no controls on it),
        // gated by the same DashboardEnabled flag since it's the same "I want this exposed" choice.
        var statusMatch = System.Text.RegularExpressions.Regex.Match(path, @"^/status/([^/]+)$");
        if (req.HttpMethod == "GET" && statusMatch.Success)
        {
            if (!DashboardEnabled) { resp.StatusCode = 403; resp.Close(); return; }
            var srv = GetServers?.Invoke().FirstOrDefault(s => s.Id == statusMatch.Groups[1].Value);
            if (srv == null) { resp.StatusCode = 404; await SendHtml(resp, "<html><body style='background:#0d1117;color:#8b949e;font-family:sans-serif'>Server not found.</body></html>"); return; }
            await SendHtml(resp, BuildStatusHtml(srv));
            return;
        }

        // Public "wake server" trigger from the status page — no auth, but gated by the
        // server's own WakeOnDemand opt-in flag, only works while stopped, and rate-limited
        // per server so the unauthenticated link can't be used to spam start/stop.
        var wakeMatch = System.Text.RegularExpressions.Regex.Match(path, @"^/status/([^/]+)/wake$");
        if (req.HttpMethod == "POST" && wakeMatch.Success)
        {
            if (!DashboardEnabled) { resp.StatusCode = 403; await SendJson(resp, new { error = "Disabled" }); return; }
            var id  = wakeMatch.Groups[1].Value;
            var srv = GetServers?.Invoke().FirstOrDefault(s => s.Id == id);
            if (srv == null) { resp.StatusCode = 404; await SendJson(resp, new { error = "Not found" }); return; }
            if (!srv.WakeOnDemand) { resp.StatusCode = 403; await SendJson(resp, new { error = "Wake on demand is not enabled for this server" }); return; }
            if (srv.Status != ServerStatus.Stopped) { await SendJson(resp, new { ok = true, alreadyRunning = true }); return; }

            bool tooSoon;
            lock (_lastPublicWake)
            {
                tooSoon = _lastPublicWake.TryGetValue(id, out var last) && DateTime.UtcNow - last < PublicWakeCooldown;
                if (!tooSoon) _lastPublicWake[id] = DateTime.UtcNow;
            }
            if (tooSoon)
            {
                resp.StatusCode = 429;
                await SendJson(resp, new { error = "Please wait a moment before trying again" });
                return;
            }

            await (StartServer?.Invoke(id) ?? Task.CompletedTask);
            await SendJson(resp, new { ok = true });
            return;
        }

        // Auth check for API
        // Two accepted forms:
        //   1. Config API token (Token property) → full access (master↔slave, legacy)
        //   2. User-database token → role-based access (web dashboard)
        WgsUser? authedUser = null;
        if (path.StartsWith("/api"))
        {
            var raw   = req.Headers["Authorization"] ?? req.QueryString["token"] ?? string.Empty;
            var token = raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? raw[7..] : raw;

            bool isApiToken  = token.Equals(Token, StringComparison.OrdinalIgnoreCase);
            bool isUserToken = !isApiToken && (Users?.ValidateToken(token, out authedUser) ?? false);

            if (!isApiToken && !isUserToken)
            {
                resp.StatusCode = 401;
                await SendJson(resp, new { error = "Unauthorized" });
                return;
            }
            // isApiToken → authedUser == null → full access (no Viewer restriction)
        }

        // Viewer role can't change server state (only applies to user-account logins)
        bool isViewer = authedUser?.Role == UserRole.Viewer;

        try
        {
            if (req.HttpMethod == "GET" && path == "/api/servers")
            {
                var servers = GetServers?.Invoke() ?? [];
                await SendJson(resp, servers.Select(s => new {
                    s.Id, s.DisplayName, s.GameId, Status = s.Status.ToString(),
                    s.ServerPort, s.MaxPlayers, s.CurrentPlayers,
                    Notes = s.Notes ?? "" }));
                return;
            }

            // GET /api/servers/{id}
            var idMatch = System.Text.RegularExpressions.Regex.Match(path, @"^/api/servers/([^/]+)$");
            if (req.HttpMethod == "GET" && idMatch.Success)
            {
                var sid = idMatch.Groups[1].Value;
                var srv = GetServers?.Invoke().FirstOrDefault(s => s.Id == sid);
                if (srv == null) { resp.StatusCode = 404; await SendJson(resp, new { error = "Not found" }); return; }
                var uptime  = GetUptime?.Invoke(sid);
                var players = GetOnlinePlayers?.Invoke(sid)?.Select(p => new {
                    p.Name, p.SteamId, p.Ping, p.ConnectedSeconds, p.ConnectedText }) ?? [];
                await SendJson(resp, new {
                    srv.Id, srv.DisplayName, srv.GameId, Status = srv.Status.ToString(),
                    srv.ServerPort, srv.MaxPlayers, srv.CurrentPlayers,
                    Uptime = uptime ?? "--:--:--",
                    Players = players,
                    Notes  = srv.Notes ?? "",
                });
                return;
            }

            // GET /api/servers/{id}/log?offset=N
            var logParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (req.HttpMethod == "GET" && logParts.Length == 4
                && logParts[0] == "api" && logParts[1] == "servers" && logParts[3] == "log")
            {
                var serverId = logParts[2];
                int.TryParse(req.QueryString["offset"], out var offset);
                var result = GetLog?.Invoke(serverId, offset) ?? (new List<string>(), new List<string>(), offset);
                await SendJson(resp, new { lines = result.lines, types = result.types, nextOffset = result.nextOffset });
                return;
            }

            // GET /api/servers/{id}/perf?minutes=N
            if (req.HttpMethod == "GET" && logParts.Length == 4
                && logParts[0] == "api" && logParts[1] == "servers" && logParts[3] == "perf")
            {
                var serverId = logParts[2];
                int.TryParse(req.QueryString["minutes"], out var minutes);
                if (minutes <= 0) minutes = 5;
                var samples = GetPerfSamples?.Invoke(serverId, minutes) ?? [];
                await SendJson(resp, samples.Select(s => new { t = s.time, cpu = Math.Round(s.cpu, 1), mem = s.memMb }));
                return;
            }

            if (req.HttpMethod == "GET" && path == "/api/system")
            {
                var m = GetMetrics?.Invoke();
                var net = GetNetwork?.Invoke();
                await SendJson(resp, m != null ? new {
                    CpuPercent   = Math.Round(m.CpuPercent, 1),
                    MemUsedGb    = Math.Round(m.MemUsedGb, 1),
                    MemTotalGb   = Math.Round(m.MemTotalGb, 1),
                    NetworkInKbs  = net.HasValue ? Math.Round(net.Value.inKbs  / 1024, 1) : (double?)null,
                    NetworkOutKbs = net.HasValue ? Math.Round(net.Value.outKbs / 1024, 1) : (double?)null,
                } : new object());
                return;
            }

            // GET /api/servers/{id}/backups
            var backupParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (req.HttpMethod == "GET" && backupParts.Length == 4
                && backupParts[0] == "api" && backupParts[1] == "servers" && backupParts[3] == "backups")
            {
                var serverId = backupParts[2];
                var backups  = GetBackups?.Invoke(serverId) ?? [];
                await SendJson(resp, backups.Select(b => new {
                    b.fileName,
                    b.sizeText,
                    createdAt = b.createdAt.ToString("yyyy-MM-dd HH:mm:ss")
                }));
                return;
            }

            // POST /api/servers/{id}/restore  body: { "fileName": "..." }
            if (req.HttpMethod == "POST" && backupParts.Length == 4
                && backupParts[0] == "api" && backupParts[1] == "servers" && backupParts[3] == "restore")
            {
                if (isViewer) { resp.StatusCode = 403; await SendJson(resp, new { error = "Forbidden" }); return; }
                var serverId = backupParts[2];
                using var reader2 = new System.IO.StreamReader(req.InputStream);
                var body2 = await reader2.ReadToEndAsync();
                var doc2  = JsonDocument.Parse(body2.Length > 0 ? body2 : "{}");
                var fileName = doc2.RootElement.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(fileName)) { resp.StatusCode = 400; await SendJson(resp, new { error = "fileName required" }); return; }
                // Reject path traversal: must be a bare filename with no directory component
                if (fileName != System.IO.Path.GetFileName(fileName)
                    || fileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                {
                    resp.StatusCode = 400;
                    await SendJson(resp, new { error = "Invalid fileName" });
                    return;
                }
                var err = RestoreBackup != null ? await RestoreBackup(serverId, fileName) : "Restore not available";
                if (err != null) { resp.StatusCode = 400; await SendJson(resp, new { error = err }); return; }
                Users?.WriteAudit(authedUser?.Username ?? "api", "restore", $"server={serverId} file={fileName}");
                await SendJson(resp, new { ok = true });
                return;
            }

            // GET /api/servers/{id}/log/download — full log as text file
            var dlParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (req.HttpMethod == "GET" && dlParts.Length == 5
                && dlParts[0] == "api" && dlParts[1] == "servers" && dlParts[3] == "log" && dlParts[4] == "download")
            {
                var serverId = dlParts[2];
                var lines    = GetFullLog?.Invoke(serverId) ?? [];
                var text     = string.Join("\n", lines);
                var bytes    = System.Text.Encoding.UTF8.GetBytes(text);
                resp.ContentType     = "text/plain; charset=utf-8";
                resp.ContentLength64 = bytes.Length;
                resp.Headers["Content-Disposition"] = $"attachment; filename=\"{serverId}_console.log\"";
                await resp.OutputStream.WriteAsync(bytes);
                resp.Close();
                return;
            }

            // GET /api/scheduled-tasks
            if (req.HttpMethod == "GET" && path == "/api/scheduled-tasks")
            {
                var tasks = GetScheduledTasks?.Invoke() ?? [];
                await SendJson(resp, tasks.Select(t => new {
                    t.Id, t.ServerId, t.ServerName,
                    action    = t.ActionText,
                    frequency = t.FrequencyText,
                    t.IsEnabled,
                    lastRun = t.LastRun?.ToString("yyyy-MM-dd HH:mm"),
                    nextRun = t.NextRun?.ToString("yyyy-MM-dd HH:mm")
                }));
                return;
            }

            // POST /api/scheduled-tasks/{id}/run
            var stParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (req.HttpMethod == "POST" && stParts.Length == 4
                && stParts[0] == "api" && stParts[1] == "scheduled-tasks" && stParts[3] == "run")
            {
                if (isViewer) { resp.StatusCode = 403; await SendJson(resp, new { error = "Forbidden" }); return; }
                var taskId = stParts[2];
                var err    = RunScheduledTask != null ? await RunScheduledTask(taskId) : "Not available";
                if (err != null) { resp.StatusCode = 400; await SendJson(resp, new { error = err }); return; }
                Users?.WriteAudit(authedUser?.Username ?? "api", "run_scheduled_task", $"id={taskId}");
                await SendJson(resp, new { ok = true });
                return;
            }

            // PATCH /api/servers/{id}/notes  body: { "notes": "..." }
            var notesParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (req.HttpMethod == "PATCH" && notesParts.Length == 4
                && notesParts[0] == "api" && notesParts[1] == "servers" && notesParts[3] == "notes")
            {
                if (isViewer) { resp.StatusCode = 403; await SendJson(resp, new { error = "Forbidden" }); return; }
                var serverId = notesParts[2];
                using var nr = new System.IO.StreamReader(req.InputStream);
                var nbody    = await nr.ReadToEndAsync();
                var ndoc     = JsonDocument.Parse(nbody.Length > 0 ? nbody : "{}");
                var notes    = ndoc.RootElement.TryGetProperty("notes", out var nv) ? nv.GetString() ?? "" : "";
                if (notes.Length > 2000) notes = notes[..2000];
                var nerr = SaveNote != null ? await SaveNote(serverId, notes) : "Not available";
                if (nerr != null) { resp.StatusCode = 400; await SendJson(resp, new { error = nerr }); return; }
                await SendJson(resp, new { ok = true });
                return;
            }

            // POST /api/servers/{id}/{action}
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (req.HttpMethod == "POST" && parts.Length >= 3 && parts[0] == "api" && parts[1] == "servers")
            {
                var id     = parts[2];
                var action = parts.Length >= 4 ? parts[3] : string.Empty;

                // Viewer ei saa käynnistää, pysäyttää, varmuuskopioida eikä lähettää komentoja
                var writeActions = new[] { "start", "stop", "restart", "update", "cmd", "backup" }; // #3: backup lisätty
                if (isViewer && Array.IndexOf(writeActions, action) >= 0)
                {
                    resp.StatusCode = 403;
                    await SendJson(resp, new { error = "Forbidden: Viewer role cannot modify server state" });
                    return;
                }

                switch (action)
                {
                    case "start":   await (StartServer?.Invoke(id) ?? Task.CompletedTask); break;
                    case "stop":    await (StopServer?.Invoke(id)  ?? Task.CompletedTask); break;
                    case "restart": await (RestartServer?.Invoke(id) ?? Task.CompletedTask); break;
                    case "update":  await (UpdateServer?.Invoke(id) ?? Task.CompletedTask); break;
                    case "backup":  await (BackupServer?.Invoke(id) ?? Task.CompletedTask); break;
                    case "cmd":
                        using (var reader = new System.IO.StreamReader(req.InputStream))
                        {
                            var body = await reader.ReadToEndAsync();
                            var doc  = JsonDocument.Parse(body.Length > 0 ? body : "{}");
                            var cmd  = doc.RootElement.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(cmd))
                                Users?.WriteAudit(authedUser?.Username ?? "api", "console_cmd",
                                    $"server={id} cmd={cmd}");
                            await (SendCmd?.Invoke(id, cmd) ?? Task.CompletedTask);
                        }
                        break;
                    default:
                        resp.StatusCode = 404;
                        await SendJson(resp, new { error = "Unknown action" });
                        return;
                }
                if (action != "cmd")
                    Users?.WriteAudit(authedUser?.Username ?? "api", action, $"server={id}");
                await SendJson(resp, new { ok = true });
                return;
            }

            resp.StatusCode = 404;
            await SendJson(resp, new { error = "Not found" });
        }
        catch (Exception ex)
        {
            resp.StatusCode = 500;
            try { await SendJson(resp, new { error = ex.Message }); } catch { }
        }
        finally
        {
            try { resp.Close(); } catch { }
        }
    }

    private static async Task SendJson(HttpListenerResponse resp, object data)
    {
        var json  = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
        var bytes = Encoding.UTF8.GetBytes(json);
        resp.ContentType     = "application/json";
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes);
        resp.Close();
    }

    private static byte[]? _logoBytes;

    private static async Task SendLogoAsync(HttpListenerResponse resp)
    {
        try
        {
            _logoBytes ??= ReadLogoBytes();
            if (_logoBytes == null) { resp.StatusCode = 404; resp.Close(); return; }
            resp.ContentType     = "image/x-icon";
            resp.ContentLength64 = _logoBytes.Length;
            resp.Headers["Cache-Control"] = "public, max-age=86400";
            await resp.OutputStream.WriteAsync(_logoBytes);
            resp.Close();
        }
        catch { resp.StatusCode = 500; resp.Close(); }
    }

    private static byte[]? ReadLogoBytes()
    {
        // favicon.ico ships as a WPF "Resource" (pack URI), bundled inside the exe rather than
        // copied to the publish folder as a loose file. Using the same icon as the app's own
        // window/taskbar icon, not the "WGS" text logo, since the shield reads more clearly at
        // the small sizes browser tabs and inline headers render it at.
        try
        {
            var uri    = new Uri("pack://application:,,,/favicon.ico", UriKind.Absolute);
            var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream == null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch { return null; }
    }

    private static async Task SendHtml(HttpListenerResponse resp, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        resp.ContentType     = "text/html; charset=utf-8";
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes);
        resp.Close();
    }

    /// <summary>Simple, read-only, shareable status page — no controls, safe to post in Discord.</summary>
    private string BuildStatusHtml(Models.GameServer srv)
    {
        var plugin   = Games.GameRegistry.Get(srv.GameId);
        var gameName = plugin?.GameName ?? srv.GameId;
        var isRunning = srv.Status == Models.ServerStatus.Running;
        var uptime   = isRunning ? (GetUptime?.Invoke(srv.Id) ?? "--:--:--") : null;
        var players  = isRunning ? (GetOnlinePlayers?.Invoke(srv.Id)?.ToList() ?? []) : [];
        string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);

        var playerRows = players.Count > 0
            ? string.Join("", players.Select(p => $"<li>{Esc(p.Name)}</li>"))
            : "<li style=\"color:#8b949e\">No players online</li>";

        var showWakeButton = !isRunning && srv.WakeOnDemand;
        var wakeButtonHtml = showWakeButton ? BuildWakeButtonHtml(srv.Id) : "";

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<link rel="icon" type="image/x-icon" href="/favicon.ico">
<title>{{Esc(srv.DisplayName)}} — Server Status</title>
<style>
body{font-family:-apple-system,Segoe UI,sans-serif;background:#0d1117;color:#c9d1d9;margin:0;padding:40px 16px;display:flex;justify-content:center}
.card{background:#161b22;border:1px solid #30363d;border-radius:10px;padding:28px 32px;max-width:420px;width:100%}
h1{font-size:20px;margin:0 0 4px;color:#f0f6fc}
.sub{color:#8b949e;font-size:13px;margin-bottom:18px}
.dot{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:6px;background:{{(isRunning ? "#3fb950" : "#6e7681")}}}
.stat{display:flex;justify-content:space-between;padding:8px 0;border-top:1px solid #21262d;font-size:14px}
ul{list-style:none;padding:0;margin:10px 0 0}
li{padding:4px 0;font-size:13px;color:#e6edf3}
.footer{margin-top:18px;font-size:11px;color:#6e7681;text-align:center}
.wake-btn{width:100%;margin-top:16px;background:#1f6feb;border:none;border-radius:6px;padding:10px;color:#fff;font-size:14px;font-weight:600;cursor:pointer}
.wake-btn:disabled{background:#30363d;color:#8b949e;cursor:default}
.wake-msg{margin-top:8px;font-size:12px;color:#8b949e;text-align:center;min-height:16px}
</style>
</head>
<body>
<div class="card">
  <h1><img src="/favicon.ico" alt="WGS" style="height:24px;vertical-align:middle;margin-right:8px;border-radius:4px"/>{{Esc(srv.DisplayName)}}</h1>
  <div class="sub">{{Esc(gameName)}}</div>
  <div class="stat"><span><span class="dot"></span>Status</span><span>{{(isRunning ? "Online" : "Offline")}}</span></div>
  {{(isRunning ? $"""<div class="stat"><span>Uptime</span><span>{uptime}</span></div>""" : "")}}
  <div class="stat"><span>Players</span><span>{{players.Count}}/{{srv.MaxPlayers}}</span></div>
  <ul>{{playerRows}}</ul>
  {{wakeButtonHtml}}
  <div class="footer">Powered by Windows Game Server</div>
</div>
</body>
</html>
""";
    }

    private static string BuildWakeButtonHtml(string serverId)
    {
        var escId = System.Net.WebUtility.HtmlEncode(serverId);
        return $$"""
  <button class="wake-btn" id="wakeBtn" onclick="wake()">Wake server</button>
  <div class="wake-msg" id="wakeMsg"></div>
  <script>
  function wake(){
    var btn=document.getElementById('wakeBtn'), msg=document.getElementById('wakeMsg');
    btn.disabled=true; btn.textContent='Starting...';
    fetch('/status/{{escId}}/wake',{method:'POST'})
      .then(function(r){return r.json().then(function(j){return {ok:r.ok,j:j};});})
      .then(function(res){
        if(res.ok){msg.textContent='Starting up — give it a minute, then connect normally.';}
        else{btn.disabled=false; btn.textContent='Wake server'; msg.textContent=res.j.error||'Could not start the server.';}
      })
      .catch(function(){btn.disabled=false; btn.textContent='Wake server'; msg.textContent='Request failed.';});
  }
  </script>
""";
    }

    private string BuildUiHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<link rel="icon" type="image/x-icon" href="/favicon.ico">
<title>WGS Dashboard</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Segoe UI',system-ui,sans-serif;background:#0d1117;color:#e6edf3;min-height:100vh}
/* Header */
.hdr{background:#161b22;border-bottom:1px solid #30363d;padding:12px 24px;display:flex;align-items:center;gap:12px;position:sticky;top:0;z-index:20}
.hdr-title{font-size:16px;font-weight:700;letter-spacing:-.3px}
.hdr-right{margin-left:auto;display:flex;align-items:center;gap:10px;font-size:12px;color:#8b949e}
.badge{border-radius:6px;padding:3px 10px;font-size:12px;border:1px solid #30363d;color:#8b949e;background:#21262d}
.badge.ok{border-color:#238636;color:#3fb950;background:#0d2116}
.badge.err{border-color:#da3633;color:#f85149;background:#2a0f0f}
/* Auth */
.auth-wrap{display:flex;align-items:center;justify-content:center;min-height:calc(100vh - 49px)}
.auth-card{background:#161b22;border:1px solid #30363d;border-radius:12px;padding:36px 40px;width:380px}
.auth-card h2{font-size:18px;margin-bottom:20px}
.field{margin-bottom:14px}
.field label{display:block;font-size:12px;color:#8b949e;margin-bottom:5px}
.field input{width:100%;background:#0d1117;border:1px solid #30363d;border-radius:6px;padding:9px 12px;color:#e6edf3;font-size:14px;outline:none;transition:border-color .15s}
.field input:focus{border-color:#58a6ff}
.btn-full{width:100%;background:#238636;border:none;border-radius:6px;padding:10px;color:#fff;cursor:pointer;font-size:14px;font-weight:600}
.btn-full:hover{background:#2ea043}
/* Main layout */
.main{padding:20px 24px;max-width:1440px;margin:0 auto}
/* System bar */
.sysbar{display:grid;grid-template-columns:repeat(auto-fill,minmax(140px,1fr));gap:10px;margin-bottom:22px}
.sysc{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:14px 16px}
.sysc .lbl{font-size:10px;color:#8b949e;text-transform:uppercase;letter-spacing:.6px;margin-bottom:4px}
.sysc .val{font-size:20px;font-weight:700}
.sysc .sub{font-size:11px;color:#8b949e;margin-top:2px}
/* Section header */
.sec-hdr{display:flex;align-items:center;justify-content:space-between;margin-bottom:14px}
.sec-hdr h2{font-size:13px;font-weight:600;color:#8b949e;text-transform:uppercase;letter-spacing:.5px}
.sortsel{background:#161b22;color:#c9d1d9;border:1px solid #30363d;border-radius:6px;padding:5px 8px;font-size:12px;cursor:pointer}
/* Server grid */
.srv-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(360px,1fr));gap:14px}
.srv{background:#161b22;border:1px solid #30363d;border-radius:10px;overflow:hidden;transition:border-color .15s}
.srv:hover{border-color:#58a6ff44}
/* Server card top */
.srv-top{padding:16px 16px 12px}
.srv-hdr{display:flex;align-items:flex-start;gap:10px}
.dot{width:9px;height:9px;border-radius:50%;margin-top:5px;flex-shrink:0;box-shadow:0 0 6px currentColor}
.dot.Running{color:#3fb950;background:#3fb950}
.dot.Stopped,.dot.NotInstalled{color:#8b949e;background:#8b949e}
.dot.Error{color:#f85149;background:#f85149}
.dot.Starting,.dot.Stopping,.dot.Installing,.dot.Updating{color:#58a6ff;background:#58a6ff}
.srv-name{font-weight:600;font-size:14px}
.srv-meta{font-size:11px;color:#8b949e;margin-top:2px}
.srv-uptime{font-size:11px;color:#3fb950;font-family:Consolas,monospace;margin-top:2px}
/* Stats row */
.srv-stats{display:flex;gap:16px;margin-top:10px;flex-wrap:wrap}
.stat{font-size:11px;color:#8b949e;display:flex;flex-direction:column;gap:2px}
.stat span{color:#e6edf3;font-weight:600;font-size:13px}
/* Mini perf sparkline */
.spark{width:100%;height:28px;margin-top:8px;display:none}
/* Player pills */
.players-row{margin-top:8px;display:flex;flex-wrap:wrap;gap:4px;min-height:0}
.player-pill{background:#21262d;border:1px solid #30363d;border-radius:4px;padding:2px 7px;font-size:11px;color:#c9d1d9}
.player-pill .ping{color:#8b949e;font-size:10px}
/* Action buttons */
.srv-btns{padding:10px 16px;display:flex;flex-wrap:wrap;gap:5px;border-top:1px solid #21262d}
.btn{border:none;border-radius:6px;padding:5px 11px;cursor:pointer;font-size:12px;font-weight:500;transition:opacity .15s}
.btn:disabled{opacity:.35;cursor:not-allowed}
.btn:hover:not(:disabled){opacity:.85}
.bg{background:#238636;color:#fff}
.br{background:#da3633;color:#fff}
.bb{background:#1f6feb;color:#fff}
.bo{background:#21262d;color:#e6edf3;border:1px solid #30363d}
/* Console row */
.con-row{padding:8px 16px 10px;display:flex;gap:6px;border-top:1px solid #21262d}
.con-row input{flex:1;background:#0d1117;border:1px solid #30363d;border-radius:6px;padding:5px 10px;color:#e6edf3;font-size:12px;font-family:Consolas,monospace;outline:none;transition:border-color .15s}
.con-row input:focus{border-color:#58a6ff}
/* Log panel */
.log-toggle{background:none;border:none;color:#8b949e;font-size:11px;cursor:pointer;padding:4px 16px 8px;display:block;width:100%;text-align:left}
.log-toggle:hover{color:#e6edf3}
.log-box{display:none;background:#0d1117;border-top:1px solid #21262d;padding:10px 14px;max-height:200px;overflow-y:auto;font-family:Consolas,monospace;font-size:11px;line-height:1.5}
.log-box.open{display:block}
.log-line{color:#8b949e;white-space:pre-wrap;word-break:break-all}
.log-line.System{color:#58a6ff}
.log-line.Warning{color:#d29922}
.log-line.Error{color:#f85149}
/* Backup row */
#bkrow_* div:last-child{border-bottom:none}
/* Toast */
.toast{position:fixed;bottom:20px;right:20px;background:#238636;color:#fff;padding:10px 18px;border-radius:8px;font-size:13px;opacity:0;transition:opacity .25s;pointer-events:none;z-index:99}
.toast.show{opacity:1}
.toast.err-t{background:#da3633}
/* Notes */
.notes-wrap{padding:6px 16px 10px;border-top:1px solid #21262d}
.notes-toggle{background:none;border:none;color:#8b949e;font-size:11px;cursor:pointer;padding:4px 16px 8px;display:block;width:100%;text-align:left}
.notes-toggle:hover{color:#e6edf3}
.notes-area{display:none;flex-direction:column;gap:5px}
.notes-area.open{display:flex}
.notes-area textarea{width:100%;background:#0d1117;border:1px solid #30363d;border-radius:6px;padding:7px 10px;color:#e6edf3;font-size:12px;font-family:'Segoe UI',system-ui,sans-serif;resize:vertical;min-height:64px;outline:none;transition:border-color .15s}
.notes-area textarea:focus{border-color:#58a6ff}
.notes-save{align-self:flex-end;background:#238636;border:none;border-radius:6px;padding:4px 14px;color:#fff;font-size:12px;cursor:pointer}
.notes-save:hover{background:#2ea043}
/* Scheduled tasks */
.sched-section{margin-top:22px}
.sched-list{display:flex;flex-direction:column;gap:8px}
.sched-item{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:12px 16px;display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.sched-item .si-name{font-size:13px;font-weight:600;flex:1;min-width:120px}
.sched-item .si-meta{font-size:11px;color:#8b949e;display:flex;gap:14px;flex-wrap:wrap}
.sched-item .si-tag{background:#21262d;border:1px solid #30363d;border-radius:4px;padding:2px 8px;font-size:11px;color:#c9d1d9}
.sched-item .si-disabled{opacity:.45}
/* Player list panel */
.players-panel{padding:6px 16px 10px;border-top:1px solid #21262d;display:none}
.players-panel.open{display:block}
.players-toggle{background:none;border:none;color:#8b949e;font-size:11px;cursor:pointer;padding:4px 16px 8px;display:block;width:100%;text-align:left}
.players-toggle:hover{color:#e6edf3}
.player-row{display:flex;gap:8px;padding:4px 0;border-bottom:1px solid #21262d18;font-size:12px;align-items:center}
.player-row:last-child{border-bottom:none}
.player-row .pn{color:#e6edf3;flex:1}
.player-row .pp{color:#8b949e;font-size:11px;width:48px;text-align:right}
.player-row .pt{color:#8b949e;font-size:11px;width:64px;text-align:right}
/* Responsive */
@media(max-width:640px){
  .hdr{padding:10px 14px}
  .main{padding:12px 14px}
  .sysbar{grid-template-columns:repeat(2,1fr)}
  .srv-grid{grid-template-columns:1fr}
  .srv-btns{gap:4px}
  .btn{padding:7px 10px;font-size:12px;flex:1 1 auto}
  .sched-item{flex-direction:column;align-items:flex-start}
  .si-meta{flex-direction:column;gap:4px}
}
@media(max-width:360px){
  .sysbar{grid-template-columns:1fr 1fr}
  .hdr-title{font-size:14px}
}
</style>
</head>
<body>
<div class="hdr">
  <div class="hdr-title"><img src="/favicon.ico" alt="WGS" style="height:28px;vertical-align:middle;margin-right:8px"/>Dashboard</div>
  <div class="hdr-right">
    <span id="refreshTxt"></span>
    <span class="badge" id="conBadge">Connecting…</span>
  </div>
</div>

<!-- Auth screen -->
<div id="authWrap" class="auth-wrap">
  <div class="auth-card">
    <h2><img src="/favicon.ico" alt="WGS" style="height:32px;vertical-align:middle;margin-right:8px"/>Dashboard</h2>
    <div class="field"><label>Access Token</label>
      <input type="password" id="tokInp" placeholder="Enter token…"
             onkeydown="if(event.key==='Enter')connect()">
    </div>
    <button class="btn-full" onclick="connect()">Connect</button>
  </div>
</div>

<!-- App -->
<div id="appWrap" style="display:none">
  <div class="main">
    <!-- System metrics -->
    <div class="sysbar">
      <div class="sysc"><div class="lbl">CPU</div><div class="val" id="sCpu">—</div></div>
      <div class="sysc"><div class="lbl">Memory</div><div class="val" id="sMem">—</div><div class="sub" id="sMemSub"></div></div>
      <div class="sysc"><div class="lbl">Running</div><div class="val" id="sRun">—</div><div class="sub" id="sTotal"></div></div>
      <div class="sysc"><div class="lbl">BW ↓</div><div class="val" id="sNetIn">—</div></div>
      <div class="sysc"><div class="lbl">BW ↑</div><div class="val" id="sNetOut">—</div></div>
    </div>
    <!-- Servers -->
    <div class="sec-hdr">
      <h2>Servers</h2>
      <select class="sortsel" id="sortSel" onchange="setSortMode(this.value)">
        <option value="name-asc">A → Z</option>
        <option value="name-desc">Z → A</option>
        <option value="game-asc">Game</option>
        <option value="status">Status</option>
      </select>
    </div>
    <div class="srv-grid" id="srvGrid"></div>

    <!-- Scheduled tasks -->
    <div class="sched-section" id="schedSection" style="display:none">
      <div class="sec-hdr" style="margin-top:10px">
        <h2>Scheduled Tasks</h2>
      </div>
      <div class="sched-list" id="schedList"></div>
    </div>
  </div>
</div>

<div class="toast" id="toast"></div>

<script>
let TOKEN='', refreshIv=null, logOffsets={}, logOpen={};

// ── Auth ──────────────────────────────────────────────────────────────────
function connect(){
  TOKEN=(document.getElementById('tokInp').value||'').trim();
  if(TOKEN){localStorage.setItem('wgs_token',TOKEN);loadAll();}
}
(function(){
  const t=localStorage.getItem('wgs_token');
  if(t){TOKEN=t;document.getElementById('tokInp').value=t;}
})();

// ── API ───────────────────────────────────────────────────────────────────
async function api(path,method,body){
  const r=await fetch('/api/'+path,{
    method:method||'GET',
    headers:{'Authorization':'Bearer '+TOKEN,'Content-Type':'application/json'},
    body:body?JSON.stringify(body):null
  });
  if(!r.ok)throw r.status;
  return r.json();
}

// ── Main refresh ─────────────────────────────────────────────────────────
async function loadAll(){
  try{
    const[sv,sys]=await Promise.all([api('servers'),api('system')]);
    loadScheduled();
    document.getElementById('conBadge').textContent='Connected';
    document.getElementById('conBadge').className='badge ok';
    document.getElementById('authWrap').style.display='none';
    document.getElementById('appWrap').style.display='';
    const sortSel=document.getElementById('sortSel');
    if(sortSel&&sortSel.value!==sortMode)sortSel.value=sortMode;
    renderSys(sys,sv);
    renderServers(sv);
    document.getElementById('refreshTxt').textContent='Updated '+new Date().toLocaleTimeString('en');
    if(!refreshIv)refreshIv=setInterval(loadAll,5000);
  }catch(e){
    document.getElementById('conBadge').textContent='No connection';
    document.getElementById('conBadge').className='badge err';
  }
}

// ── System bar ───────────────────────────────────────────────────────────
function fmtNet(v){if(v==null)return'—';return v<1024?v.toFixed(0)+' KB/s':(v/1024).toFixed(1)+' MB/s';}
function renderSys(s,sv){
  document.getElementById('sCpu').textContent  =s.CpuPercent!=null?s.CpuPercent.toFixed(1)+'%':'—';
  document.getElementById('sMem').textContent  =s.MemUsedGb!=null?s.MemUsedGb.toFixed(1)+' GB':'—';
  document.getElementById('sMemSub').textContent=s.MemTotalGb?'/ '+s.MemTotalGb.toFixed(1)+' GB':'';
  const run=sv.filter(x=>x.Status==='Running').length;
  document.getElementById('sRun').textContent   =run;
  document.getElementById('sTotal').textContent ='/ '+sv.length+' total';
  document.getElementById('sNetIn').textContent =fmtNet(s.NetworkInKbs);
  document.getElementById('sNetOut').textContent=fmtNet(s.NetworkOutKbs);
}

// ── Server cards ──────────────────────────────────────────────────────────
function sc(s){return{Running:'Running',Stopped:'Stopped',NotInstalled:'Stopped',
  Error:'Error',Starting:'Starting',Stopping:'Stopping',
  Installing:'Installing',Updating:'Updating'}[s]||'Stopped';}
function esc(s){return(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
function statusLabel(s){return{Running:'Running',Stopped:'Stopped',NotInstalled:'Not installed',
  Error:'Error',Starting:'Starting…',Stopping:'Stopping…',
  Installing:'Installing…',Updating:'Updating…'}[s]||s;}

let sortMode=localStorage.getItem('wgsSortMode')||'name-asc';
function setSortMode(v){sortMode=v;localStorage.setItem('wgsSortMode',v);if(lastServerList)renderServers(lastServerList);}

const statusOrder={Running:0,Starting:1,Updating:1,Installing:1,Stopping:2,Stopped:3,Error:4,NotInstalled:5};
function sortServers(list){
  const arr=[...list];
  switch(sortMode){
    case 'name-desc': arr.sort((a,b)=>b.DisplayName.localeCompare(a.DisplayName)); break;
    case 'game-asc':  arr.sort((a,b)=>a.GameId.localeCompare(b.GameId)); break;
    case 'status':    arr.sort((a,b)=>(statusOrder[a.Status]??9)-(statusOrder[b.Status]??9)); break;
    default:          arr.sort((a,b)=>a.DisplayName.localeCompare(b.DisplayName));
  }
  return arr;
}

let lastServerList=null;
function renderServers(list){
  lastServerList=list;
  const sorted=sortServers(list);
  const grid=document.getElementById('srvGrid');
  const existing=new Set([...grid.querySelectorAll('.srv')].map(el=>el.dataset.id));
  const incoming=new Set(sorted.map(s=>s.Id));
  // Remove stale cards
  existing.forEach(id=>{if(!incoming.has(id))document.querySelector('.srv[data-id="'+id+'"]')?.remove();});
  sorted.forEach(s=>upsertCard(s));
  // Reorder DOM to match sort order (appendChild moves existing nodes)
  sorted.forEach(s=>{
    const card=grid.querySelector('.srv[data-id="'+s.Id+'"]');
    if(card)grid.appendChild(card);
  });
}

function upsertCard(s){
  const grid=document.getElementById('srvGrid');
  let card=grid.querySelector('.srv[data-id="'+s.Id+'"]');
  const isNew=!card;

  const isRun=s.Status==='Running',isStop=!isRun&&s.Status!=='Starting'&&s.Status!=='Stopping';
  const isBusy=s.Status==='Starting'||s.Status==='Stopping'||s.Status==='Installing'||s.Status==='Updating';

  if(isNew){
    card=document.createElement('div');card.className='srv';card.dataset.id=s.Id;
    card.innerHTML=`
<div class="srv-top">
  <div class="srv-hdr">
    <div class="dot" id="dot_${s.Id}"></div>
    <div style="flex:1">
      <div class="srv-name">${esc(s.DisplayName)}</div>
      <div class="srv-meta" id="meta_${s.Id}"></div>
      <div class="srv-uptime" id="up_${s.Id}" style="display:none"></div>
    </div>
  </div>
  <div class="srv-stats">
    <div class="stat"><span id="pl_${s.Id}">${s.CurrentPlayers}/${s.MaxPlayers}</span>Players</div>
    <div class="stat"><span>${s.ServerPort}</span>Port</div>
  </div>
  <svg class="spark" id="spark_${s.Id}" viewBox="0 0 100 28" preserveAspectRatio="none"></svg>
  <div class="players-row" id="plrow_${s.Id}"></div>
</div>
<div class="srv-btns">
  <button class="btn bg" id="btn_start_${s.Id}" onclick="act('${s.Id}','start')">▶ Start</button>
  <button class="btn br" id="btn_stop_${s.Id}"  onclick="act('${s.Id}','stop')">■ Stop</button>
  <button class="btn bb" id="btn_restart_${s.Id}" onclick="act('${s.Id}','restart')">↺ Restart</button>
  <button class="btn bo" onclick="act('${s.Id}','update')">⬇ Update</button>
  <button class="btn bo" onclick="act('${s.Id}','backup')">💾 Backup</button>
  <button class="btn bo" onclick="toggleBackups('${s.Id}')">📂 Backups</button>
</div>
<div id="bkrow_${s.Id}" style="display:none;padding:6px 16px;border-top:1px solid #21262d"></div>
<button class="players-toggle" id="pltog_${s.Id}" onclick="togglePlayers('${s.Id}')">▼ Players (0)</button>
<div class="players-panel" id="plpanel_${s.Id}"></div>
<div class="con-row">
  <input id="c_${s.Id}" placeholder="Console command…">
  <button class="btn bo" onclick="sendCmd('${s.Id}')">Send</button>
</div>
<div style="display:flex;align-items:center;justify-content:space-between;padding:0 16px">
  <button class="log-toggle" style="padding:4px 0;flex:1;text-align:left" onclick="toggleLog('${s.Id}')">▼ Console Log</button>
  <button class="btn bo" style="font-size:11px;padding:2px 8px" onclick="downloadLog('${s.Id}')" title="Download full log">⬇ Log</button>
</div>
<div class="log-box" id="log_${s.Id}"></div>
<button class="notes-toggle" onclick="toggleNotes('${s.Id}')">📝 Notes</button>
<div class="notes-wrap" style="padding-top:0">
  <div class="notes-area" id="notes_${s.Id}">
    <textarea id="ntxt_${s.Id}" placeholder="Server notes…" rows="3"></textarea>
    <button class="notes-save" onclick="saveNotes('${s.Id}')">Save</button>
  </div>
</div>
`;
    grid.appendChild(card);
  }

  // Update only dynamic fields — never rebuild the whole card
  const dot=document.getElementById('dot_'+s.Id);
  if(dot){dot.className='dot '+sc(s.Status);}
  const meta=document.getElementById('meta_'+s.Id);
  if(meta)meta.textContent=s.GameId+' · '+statusLabel(s.Status);
  const upEl=document.getElementById('up_'+s.Id);
  if(upEl)upEl.style.display=isRun?'block':'none';
  const btnStart=document.getElementById('btn_start_'+s.Id);
  if(btnStart)btnStart.disabled=isRun||isBusy;
  const btnStop=document.getElementById('btn_stop_'+s.Id);
  if(btnStop)btnStop.disabled=isStop;
  const btnRestart=document.getElementById('btn_restart_'+s.Id);
  if(btnRestart)btnRestart.disabled=!isRun;

  // Players toggle — only useful when running
  const pltogEl=document.getElementById('pltog_'+s.Id);
  if(pltogEl)pltogEl.style.display=isRun?'block':'none';
  if(!isRun){const pp=document.getElementById('plpanel_'+s.Id);if(pp)pp.classList.remove('open');}
  if(isRun){fetchDetail(s.Id);fetchPerf(s.Id);}
  else{
    const sp=document.getElementById('spark_'+s.Id);if(sp)sp.style.display='none';
    // Load notes for stopped servers once
    if(_serverNotes[s.Id]==null&&s.Notes!=null){
      _serverNotes[s.Id]=s.Notes;
      const ntxt=document.getElementById('ntxt_'+s.Id);
      if(ntxt)ntxt.value=s.Notes;
    }
  }
  if(isNew&&logOpen[s.Id])openLog(s.Id);
}

// ── Server detail (uptime + players) ─────────────────────────────────────
const _serverNotes={};
async function fetchDetail(id){
  try{
    const d=await api('servers/'+id);
    const el=document.getElementById('up_'+id);
    if(el){el.textContent='Uptime: '+d.Uptime;el.style.display='block';}
    const plEl=document.getElementById('pl_'+id);
    if(plEl)plEl.textContent=d.Players.length+'/'+d.MaxPlayers;
    // Old pills row (hidden, kept for compat)
    const row=document.getElementById('plrow_'+id);
    if(row)row.innerHTML='';
    // Player toggle label
    const pltog=document.getElementById('pltog_'+id);
    if(pltog)pltog.textContent=(d.Players.length>0?'▼':'▼')+' Players ('+d.Players.length+')';
    // Player panel (if open)
    const plpanel=document.getElementById('plpanel_'+id);
    if(plpanel&&plpanel.classList.contains('open'))renderPlayerPanel(id,d.Players,d.MaxPlayers);
    // Notes — init once
    const ntxt=document.getElementById('ntxt_'+id);
    if(ntxt&&_serverNotes[id]==null){_serverNotes[id]=d.Notes||'';ntxt.value=_serverNotes[id];}
  }catch(e){}
}

function renderPlayerPanel(id,players,maxPlayers){
  const plpanel=document.getElementById('plpanel_'+id);
  if(!plpanel)return;
  if(players.length===0){plpanel.innerHTML='<div style="font-size:12px;color:#8b949e;padding:2px 0">No players online.</div>';return;}
  plpanel.innerHTML='<div class="player-row" style="font-size:10px;color:#8b949e;font-weight:600"><span class="pn">Name</span><span class="pp">Ping</span><span class="pt">Connected</span></div>'+
    players.map(p=>`<div class="player-row"><span class="pn">${esc(p.Name)}</span><span class="pp">${p.Ping>0?p.Ping+'ms':'—'}</span><span class="pt">${p.ConnectedText||'—'}</span></div>`).join('');
}

function togglePlayers(id){
  const panel=document.getElementById('plpanel_'+id);
  const tog=document.getElementById('pltog_'+id);
  if(!panel)return;
  if(panel.classList.contains('open')){panel.classList.remove('open');return;}
  panel.classList.add('open');
  // Refresh immediately
  api('servers/'+id).then(d=>{
    const pltog=document.getElementById('pltog_'+id);
    if(pltog)pltog.textContent='▼ Players ('+d.Players.length+')';
    renderPlayerPanel(id,d.Players,d.MaxPlayers);
  }).catch(()=>{});
}

// ── Log download ──────────────────────────────────────────────────────────
async function downloadLog(id){
  try{
    const r=await fetch('/api/servers/'+id+'/log/download',{
      headers:{'Authorization':'Bearer '+TOKEN}
    });
    if(!r.ok){toast('Download failed: '+r.status,true);return;}
    const text=await r.text();
    const blob=new Blob([text],{type:'text/plain'});
    const url=URL.createObjectURL(blob);
    const a=document.createElement('a');
    a.href=url;a.download=id+'_console.log';
    document.body.appendChild(a);a.click();
    setTimeout(()=>{URL.revokeObjectURL(url);a.remove();},1000);
  }catch(e){toast('Download failed',true);}
}

// ── Notes ─────────────────────────────────────────────────────────────────
function toggleNotes(id){
  const area=document.getElementById('notes_'+id);
  if(!area)return;
  area.classList.toggle('open');
}
async function saveNotes(id){
  const ntxt=document.getElementById('ntxt_'+id);
  if(!ntxt)return;
  try{
    await api('servers/'+id+'/notes','PATCH',{notes:ntxt.value});
    _serverNotes[id]=ntxt.value;
    toast('Notes saved');
  }catch(e){toast('Save failed',true);}
}

// ── Scheduled tasks ───────────────────────────────────────────────────────
async function loadScheduled(){
  try{
    const tasks=await api('scheduled-tasks');
    const sec=document.getElementById('schedSection');
    const list=document.getElementById('schedList');
    if(!tasks||tasks.length===0){if(sec)sec.style.display='none';return;}
    if(sec)sec.style.display='';
    list.innerHTML=tasks.map(t=>`
<div class="sched-item${t.IsEnabled?'':' si-disabled'}">
  <div class="si-name">${esc(t.serverName)} — ${esc(t.action)}</div>
  <div class="si-meta">
    <span>${esc(t.frequency)}</span>
    ${t.lastRun?`<span>Last: ${esc(t.lastRun)}</span>`:''}
    ${t.nextRun?`<span>Next: ${esc(t.nextRun)}</span>`:''}
    ${!t.IsEnabled?'<span style="color:#f85149">Disabled</span>':''}
  </div>
  <button class="btn bo" style="white-space:nowrap" onclick="runTask('${t.Id}')">▶ Run now</button>
</div>`).join('');
  }catch(e){}
}
async function runTask(id){
  if(!confirm('Run this scheduled task now?'))return;
  try{await api('scheduled-tasks/'+id+'/run','POST');toast('Task triggered');}
  catch(e){toast('Failed: '+e,true);}
}

// ── Mini CPU sparkline ────────────────────────────────────────────────────
async function fetchPerf(id){
  try{
    const samples=await api('servers/'+id+'/perf?minutes=5');
    const sp=document.getElementById('spark_'+id);
    if(!sp)return;
    if(!samples||samples.length<2){sp.style.display='none';return;}
    sp.style.display='block';
    const w=100,h=28,n=samples.length;
    const pts=samples.map((s,i)=>{
      const x=n>1?(i/(n-1))*w:0;
      const y=h-(Math.max(0,Math.min(100,s.cpu))/100)*h;
      return x.toFixed(1)+','+y.toFixed(1);
    }).join(' ');
    sp.innerHTML=`<polyline points="${pts}" fill="none" stroke="#58a6ff" stroke-width="1.5" vector-effect="non-scaling-stroke"/>`;
  }catch(e){}
}

// ── Log viewer ────────────────────────────────────────────────────────────
function toggleLog(id){
  const box=document.getElementById('log_'+id);
  if(!box)return;
  if(box.classList.contains('open')){box.classList.remove('open');logOpen[id]=false;}
  else{openLog(id);}
}
function openLog(id){
  const box=document.getElementById('log_'+id);
  if(!box)return;
  box.classList.add('open');
  logOpen[id]=true;
  pollLog(id);
}
async function pollLog(id){
  const box=document.getElementById('log_'+id);
  if(!box||!box.classList.contains('open'))return;
  try{
    const offset=logOffsets[id]||0;
    const r=await api('servers/'+id+'/log?offset='+offset);
    if(r.lines&&r.lines.length>0){
      r.lines.forEach((line,i)=>{
        const div=document.createElement('div');
        div.className='log-line '+(r.types[i]||'');
        div.textContent=line;
        box.appendChild(div);
      });
      box.scrollTop=box.scrollHeight;
      // Keep max 300 lines
      while(box.children.length>300)box.removeChild(box.firstChild);
    }
    logOffsets[id]=r.nextOffset;
  }catch(e){}
  setTimeout(()=>pollLog(id),2000);
}

// ── Backup list ───────────────────────────────────────────────────────────
async function toggleBackups(id){
  const row=document.getElementById('bkrow_'+id);
  if(!row)return;
  if(row.style.display!=='none'){row.style.display='none';return;}
  row.style.display='';
  row.innerHTML='<span style="font-size:12px;color:#8b949e">Loading…</span>';
  try{
    const list=await api('servers/'+id+'/backups');
    if(!list||list.length===0){row.innerHTML='<span style="font-size:12px;color:#8b949e">No backups found.</span>';return;}
    row.innerHTML=list.map(b=>`
<div style="display:flex;align-items:center;gap:8px;padding:3px 0;border-bottom:1px solid #21262d26">
  <span style="font-size:11px;color:#c9d1d9;flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap" title="${esc(b.fileName)}">${esc(b.fileName)}</span>
  <span style="font-size:11px;color:#8b949e;white-space:nowrap">${esc(b.sizeText)}</span>
  <span style="font-size:11px;color:#8b949e;white-space:nowrap">${esc(b.createdAt)}</span>
  <button class="btn bo" style="padding:2px 8px;font-size:11px" onclick="restoreBackup('${id}','${esc(b.fileName)}')">Restore</button>
</div>`).join('');
  }catch(e){row.innerHTML='<span style="font-size:12px;color:#f85149">Error loading backups</span>';}
}
async function restoreBackup(id,fileName){
  if(!confirm('Restore backup "'+fileName+'"? This will overwrite current server files.'))return;
  try{await api('servers/'+id+'/restore','POST',{fileName});toast('Restore started');}
  catch(e){toast('Restore failed: '+e,true);}
}

// ── Actions ───────────────────────────────────────────────────────────────
async function act(id,a){
  try{await api('servers/'+id+'/'+a,'POST');toast(a);setTimeout(loadAll,1500);}
  catch(e){toast('Error: '+e,true);}
}
async function sendCmd(id){
  const el=document.getElementById('c_'+id);
  if(!el||!el.value.trim())return;
  try{await api('servers/'+id+'/cmd','POST',{command:el.value});el.value='';toast('Sent');}
  catch(e){toast('Error',true);}
}

// ── Toast ─────────────────────────────────────────────────────────────────
function toast(msg,err){
  const t=document.getElementById('toast');
  t.textContent=msg;
  t.className='toast'+(err?' err-t':'')+' show';
  clearTimeout(t._tid);
  t._tid=setTimeout(()=>{t.className='toast'+(err?' err-t':'');},2500);
}

// ── Boot ──────────────────────────────────────────────────────────────────
if(TOKEN)loadAll();
</script>
</body>
</html>
""";

    public void Dispose() => Stop();
}
