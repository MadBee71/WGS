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
    public string Token       { get; private set; } = Guid.NewGuid().ToString("N");

    // Callbacks wired by MainViewModel
    public Func<IEnumerable<GameServer>>?                   GetServers    { get; set; }
    public Func<string, Task>?                              StartServer   { get; set; }
    public Func<string, Task>?                              StopServer    { get; set; }
    public Func<string, Task>?                              RestartServer { get; set; }
    public Func<string, Task>?                              UpdateServer  { get; set; }
    public Func<string, Task>?                              BackupServer  { get; set; }
    public Func<string, string, Task>?                      SendCmd       { get; set; }
    public Func<SystemMetrics>?                             GetMetrics    { get; set; }

    public void Start(int port, string token)
    {
        Stop();
        Port  = port;
        Token = token;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{port}/");
        try { _listener.Start(); }
        catch (HttpListenerException)
        {
            // Fallback to localhost only
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
        }

        _cts        = new CancellationTokenSource();
        _serverTask = Task.Run(() => ListenLoop(_cts.Token));
        IsRunning   = true;
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
        if (path.StartsWith("/api"))
        {
            var raw   = req.Headers["Authorization"] ?? req.QueryString["token"] ?? string.Empty;
            var token = raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? raw[7..] : raw;
            if (!token.Equals(Token, StringComparison.OrdinalIgnoreCase))
            {
                resp.StatusCode = 401;
                await SendJson(resp, new { error = "Unauthorized" });
                return;
            }
        }

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

            if (req.HttpMethod == "GET" && path == "/api/system")
            {
                var m = GetMetrics?.Invoke();
                await SendJson(resp, m != null ? new {
                    CpuPercent = Math.Round(m.CpuPercent, 1),
                    MemUsedGb  = Math.Round(m.MemUsedGb, 1),
                    MemTotalGb = Math.Round(m.MemTotalGb, 1),
                } : new object());
                return;
            }

            // POST /api/servers/{id}/{action}
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (req.HttpMethod == "POST" && parts.Length >= 3 && parts[0] == "api" && parts[1] == "servers")
            {
                var id     = parts[2];
                var action = parts.Length >= 4 ? parts[3] : string.Empty;
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
                            await (SendCmd?.Invoke(id, cmd) ?? Task.CompletedTask);
                        }
                        break;
                    default:
                        resp.StatusCode = 404;
                        await SendJson(resp, new { error = "Unknown action" });
                        return;
                }
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

    private string BuildUiHtml() => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>WGS Remote</title>
        <style>
          *{box-sizing:border-box;margin:0;padding:0}
          body{font-family:Segoe UI,system-ui,sans-serif;background:#0d1117;color:#e6edf3;min-height:100vh}
          .hdr{background:#161b22;border-bottom:1px solid #30363d;padding:16px 24px;display:flex;align-items:center;gap:12px}
          .hdr h1{font-size:18px;font-weight:700}
          .badge{background:#21262d;border:1px solid #30363d;border-radius:6px;padding:3px 10px;font-size:12px;color:#8b949e}
          .main{padding:24px;max-width:1200px;margin:0 auto}
          .auth{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:24px;margin-bottom:24px}
          .auth input{background:#0d1117;border:1px solid #30363d;border-radius:6px;padding:8px 12px;color:#e6edf3;width:400px;font-size:14px}
          .auth button{background:#238636;border:none;border-radius:6px;padding:8px 16px;color:#fff;cursor:pointer;font-size:14px;margin-left:8px}
          .sysbar{display:flex;gap:16px;margin-bottom:24px}
          .sysc{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:16px 20px;flex:1}
          .sysc .lbl{font-size:11px;color:#8b949e;text-transform:uppercase;letter-spacing:.5px}
          .sysc .val{font-size:24px;font-weight:700;margin-top:4px}
          .servers{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:16px}
          .srv{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:20px}
          .srv-hdr{display:flex;align-items:center;gap:10px;margin-bottom:12px}
          .dot{width:10px;height:10px;border-radius:50%}
          .dot.Running{background:#3fb950}.dot.Stopped,.dot.NotInstalled{background:#8b949e}
          .dot.Error{background:#f85149}.dot.Starting,.dot.Stopping{background:#d29922}
          .srv-name{font-weight:600;font-size:15px}
          .srv-game{font-size:12px;color:#8b949e}
          .actions{display:flex;flex-wrap:wrap;gap:6px;margin-top:12px}
          .btn{border:none;border-radius:6px;padding:6px 14px;cursor:pointer;font-size:13px;font-weight:500}
          .btn-g{background:#238636;color:#fff}.btn-r{background:#da3633;color:#fff}
          .btn-b{background:#1f6feb;color:#fff}.btn-o{background:#21262d;color:#e6edf3;border:1px solid #30363d}
          .btn:disabled{opacity:.4;cursor:not-allowed}
          .cmd-row{display:flex;gap:6px;margin-top:10px}
          .cmd-row input{flex:1;background:#0d1117;border:1px solid #30363d;border-radius:6px;padding:6px 10px;color:#e6edf3;font-size:13px;font-family:Consolas,monospace}
          .toast{position:fixed;bottom:24px;right:24px;background:#238636;color:#fff;padding:12px 20px;border-radius:8px;opacity:0;transition:opacity .3s;pointer-events:none;font-size:14px}
          .toast.show{opacity:1}
        </style>
        </head>
        <body>
        <div class="hdr">
          <h1>WGS</h1>
          <span class="badge">Remote Control</span>
          <span class="badge" id="conStatus">Not connected</span>
        </div>
        <div class="main">
          <div class="auth" id="authBox">
            <div style="margin-bottom:12px;font-weight:600">Authentication</div>
            <input type="password" id="tokenInput" placeholder="Enter access token..." value="WGS_TOKEN_PLACEHOLDER">
            <button onclick="connect()">Connect</button>
          </div>
          <div class="sysbar" id="sysbar" style="display:none">
            <div class="sysc"><div class="lbl">CPU</div><div class="val" id="sCpu">—</div></div>
            <div class="sysc"><div class="lbl">Memory</div><div class="val" id="sMem">—</div></div>
          </div>
          <div class="servers" id="serverList"></div>
        </div>
        <div class="toast" id="toast"></div>
        <script>
        let TOKEN='',POLL=null;
        function connect(){TOKEN=document.getElementById('tokenInput').value.trim();if(!TOKEN)return;document.getElementById('authBox').style.display='none';document.getElementById('sysbar').style.display='flex';document.getElementById('conStatus').textContent='Connected';loadAll();POLL=setInterval(loadAll,5000);}
        async function api(path,method='GET',body=null){const r=await fetch('/api/'+path,{method,headers:{'Authorization':'Bearer '+TOKEN,'Content-Type':'application/json'},body:body?JSON.stringify(body):null});return r.json();}
        async function loadAll(){try{const[servers,sys]=await Promise.all([api('servers'),api('system')]);renderSys(sys);renderServers(servers);}catch(e){}}
        function renderSys(s){document.getElementById('sCpu').textContent=s.cpuPercent?.toFixed(1)+'%'||'—';document.getElementById('sMem').textContent=(s.memUsedGb?.toFixed(1)||'—')+' / '+(s.memTotalGb?.toFixed(1)||'—')+' GB';}
        function statusColor(s){return{Running:'Running',Stopped:'Stopped',Error:'Error',Starting:'Starting',Stopping:'Stopping',NotInstalled:'Stopped'}[s]||'Stopped';}
        function renderServers(list){
          const el=document.getElementById('serverList');
          el.innerHTML=list.map(s=>`
          <div class="srv">
            <div class="srv-hdr"><div class="dot ${statusColor(s.status)}"></div><div><div class="srv-name">${s.displayName}</div><div class="srv-game">${s.gameId} · ${s.status}</div></div></div>
            <div class="actions">
              <button class="btn btn-g" onclick="act('${s.id}','start')" ${s.status==='Running'?'disabled':''}>▶ Start</button>
              <button class="btn btn-r" onclick="act('${s.id}','stop')"  ${s.status!=='Running'?'disabled':''}>■ Stop</button>
              <button class="btn btn-b" onclick="act('${s.id}','restart')">↺ Restart</button>
              <button class="btn btn-o" onclick="act('${s.id}','update')">↑ Update</button>
              <button class="btn btn-o" onclick="act('${s.id}','backup')">💾 Backup</button>
            </div>
            <div class="cmd-row">
              <input id="cmd_${s.id}" placeholder="Console command..." onkeydown="if(event.key==='Enter')sendCmd('${s.id}')">
              <button class="btn btn-o" onclick="sendCmd('${s.id}')">Send</button>
            </div>
          </div>`).join('');
        }
        async function act(id,action){try{await api('servers/'+id+'/'+action,'POST');toast('✓ '+action);setTimeout(loadAll,1000);}catch(e){toast('Error: '+e);}}
        async function sendCmd(id){const el=document.getElementById('cmd_'+id);if(!el.value.trim())return;await api('servers/'+id+'/cmd','POST',{command:el.value});el.value='';toast('Command sent');}
        function toast(msg){const t=document.getElementById('toast');t.textContent=msg;t.classList.add('show');setTimeout(()=>t.classList.remove('show'),2500);}
        </script>
        </body></html>
        """.Replace("WGS_TOKEN_PLACEHOLDER", Token);

    public void Dispose() => Stop();
}
