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

    /// <summary>True when listener bound to all interfaces (reachable from network); false = localhost only.</summary>
    public bool BoundToAllInterfaces { get; private set; }

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

        // Serve UI page
        if (path == "" || path == "/" || path == "/ui")
        {
            await SendHtml(resp, BuildUiHtml());
            return;
        }

        // Auth check for API
        // Hyväksytään kaksi tapaa:
        //   1. Konfiguraatio-API-token (Token-property) → täysi pääsy (master↔slave, legacy)
        //   2. Käyttäjätietokannan token → roolikohtainen pääsy (web-dashboard)
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
            // isApiToken → authedUser == null → täysi pääsy (ei Viewer-rajoitusta)
        }

        // Viewer-rooli ei saa muuttaa palvelimen tilaa (koskee vain käyttäjätunnus-kirjautumisia)
        bool isViewer = authedUser?.Role == UserRole.Viewer;

        try
        {
            if (req.HttpMethod == "GET" && path == "/api/servers")
            {
                var servers = GetServers?.Invoke() ?? [];
                await SendJson(resp, servers.Select(s => new {
                    s.Id, s.DisplayName, s.GameId, Status = s.Status.ToString(),
                    s.ServerPort, s.MaxPlayers, s.CurrentPlayers }));
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

    private static async Task SendHtml(HttpListenerResponse resp, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        resp.ContentType     = "text/html; charset=utf-8";
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes);
        resp.Close();
    }

    private string BuildUiHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
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
/* Toast */
.toast{position:fixed;bottom:20px;right:20px;background:#238636;color:#fff;padding:10px 18px;border-radius:8px;font-size:13px;opacity:0;transition:opacity .25s;pointer-events:none;z-index:99}
.toast.show{opacity:1}
.toast.err-t{background:#da3633}
/* Responsive */
@media(max-width:600px){
  .hdr{padding:10px 14px}
  .main{padding:12px 14px}
  .sysbar{grid-template-columns:repeat(2,1fr)}
  .srv-grid{grid-template-columns:1fr}
}
</style>
</head>
<body>
<div class="hdr">
  <div class="hdr-title">WGS Dashboard</div>
  <div class="hdr-right">
    <span id="refreshTxt"></span>
    <span class="badge" id="conBadge">Connecting…</span>
  </div>
</div>

<!-- Auth screen -->
<div id="authWrap" class="auth-wrap">
  <div class="auth-card">
    <h2>WGS Dashboard</h2>
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
    </div>
    <div class="srv-grid" id="srvGrid"></div>
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
    document.getElementById('conBadge').textContent='Yhdistetty';
    document.getElementById('conBadge').className='badge ok';
    document.getElementById('authWrap').style.display='none';
    document.getElementById('appWrap').style.display='';
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

function renderServers(list){
  const grid=document.getElementById('srvGrid');
  const existing=new Set([...grid.querySelectorAll('.srv')].map(el=>el.dataset.id));
  const incoming=new Set(list.map(s=>s.Id));
  // Remove stale cards
  existing.forEach(id=>{if(!incoming.has(id))document.querySelector('.srv[data-id="'+id+'"]')?.remove();});
  list.forEach(s=>upsertCard(s));
}

function upsertCard(s){
  const grid=document.getElementById('srvGrid');
  let card=grid.querySelector('.srv[data-id="'+s.Id+'"]');
  const isNew=!card;
  if(isNew){card=document.createElement('div');card.className='srv';card.dataset.id=s.Id;grid.appendChild(card);}

  const isRun=s.Status==='Running',isStop=!isRun&&s.Status!=='Starting'&&s.Status!=='Stopping';
  const isBusy=s.Status==='Starting'||s.Status==='Stopping'||s.Status==='Installing'||s.Status==='Updating';

  card.innerHTML=`
<div class="srv-top">
  <div class="srv-hdr">
    <div class="dot ${sc(s.Status)}"></div>
    <div style="flex:1">
      <div class="srv-name">${esc(s.DisplayName)}</div>
      <div class="srv-meta">${esc(s.GameId)} &nbsp;·&nbsp; ${statusLabel(s.Status)}</div>
      <div class="srv-uptime" id="up_${s.Id}" style="display:${isRun?'block':'none'}"></div>
    </div>
  </div>
  <div class="srv-stats">
    <div class="stat"><span id="pl_${s.Id}">${s.CurrentPlayers}/${s.MaxPlayers}</span>Pelaajat</div>
    <div class="stat"><span>${s.ServerPort}</span>Portti</div>
  </div>
  <div class="players-row" id="plrow_${s.Id}"></div>
</div>
<div class="srv-btns">
  <button class="btn bg" onclick="act('${s.Id}','start')" ${isRun||isBusy?'disabled':''}>▶ Start</button>
  <button class="btn br" onclick="act('${s.Id}','stop')"  ${isStop?'disabled':''}>■ Stop</button>
  <button class="btn bb" onclick="act('${s.Id}','restart')"${!isRun?'disabled':''}>↺ Restart</button>
  <button class="btn bo" onclick="act('${s.Id}','update')">⬇ Update</button>
  <button class="btn bo" onclick="act('${s.Id}','backup')">💾 Backup</button>
</div>
<div class="con-row">
  <input id="c_${s.Id}" placeholder="Console command…">
  <button class="btn bo" onclick="sendCmd('${s.Id}')">Send</button>
</div>
<button class="log-toggle" onclick="toggleLog('${s.Id}')">▼ Console Log</button>
<div class="log-box" id="log_${s.Id}"></div>
`;
  // Fetch detail (uptime + players) async
  if(isRun)fetchDetail(s.Id);
  // Reopen log if it was open
  if(logOpen[s.Id])openLog(s.Id);
}

// ── Server detail (uptime + players) ─────────────────────────────────────
async function fetchDetail(id){
  try{
    const d=await api('servers/'+id);
    const el=document.getElementById('up_'+id);
    if(el){el.textContent='Uptime: '+d.Uptime;el.style.display='block';}
    const plEl=document.getElementById('pl_'+id);
    if(plEl)plEl.textContent=d.Players.length+'/'+d.MaxPlayers;
    const row=document.getElementById('plrow_'+id);
    if(row){
      row.innerHTML=d.Players.map(p=>
        `<span class="player-pill">${esc(p.Name)}<span class="ping"> ${p.Ping>0?p.Ping+'ms':''}</span></span>`
      ).join('');
    }
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
