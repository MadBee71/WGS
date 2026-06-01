using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using WGS.Models;

namespace WGS.Services;

public class RemoteServerInfo
{
    public string Id          { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string GameId      { get; set; } = "";
    public string Status      { get; set; } = "";
    public int    ServerPort  { get; set; }
}

public class RemoteMachineService : IDisposable
{
    private readonly string _machinesFile;
    private readonly List<MachineDefinition> _machines = new();
    private readonly Dictionary<string, MachineDefinition> _byId = new();  // O(1) lookup
    private readonly Dictionary<string, bool> _onlineStatus = new();
    private readonly HttpClient _http;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public event Action<string, List<RemoteServerInfo>>? MachineUpdated;
    public event Action? MachinesChanged;

    public IReadOnlyList<MachineDefinition> Machines => _machines;

    public RemoteMachineService(ConfigService config)
    {
        _machinesFile = Path.Combine(config.AppDataPath, "machines.json");
        _http         = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        Load();
        // Polling starts via Initialize() called from App.xaml.cs after DI is fully built
    }

    public void Initialize() => StartPolling();

    private void Load()
    {
        if (!File.Exists(_machinesFile)) return;
        try
        {
            var list = JsonConvert.DeserializeObject<List<MachineDefinition>>(File.ReadAllText(_machinesFile));
            if (list == null) return;
            foreach (var m in list) Register(m);
        }
        catch { }
    }

    private void Save()
        => File.WriteAllText(_machinesFile, JsonConvert.SerializeObject(_machines, Formatting.Indented));

    private void Register(MachineDefinition def)
    {
        // Normalize URL once so every request reuses it
        def.Url = def.Url.TrimEnd('/');
        _machines.Add(def);
        _byId[def.Id] = def;
    }

    public void AddMachine(MachineDefinition def)
    {
        Register(def);
        Save();
        MachinesChanged?.Invoke();
    }

    public void RemoveMachine(string id)
    {
        _machines.RemoveAll(m => m.Id == id);
        _byId.Remove(id);
        Save();
        MachinesChanged?.Invoke();
    }

    public bool IsOnline(string id) => _onlineStatus.TryGetValue(id, out var v) && v;

    private void StartPolling()
    {
        _cts      = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoop(_cts.Token));
    }

    private async Task PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Snapshot list once per cycle, then poll all machines in parallel
            var enabled = _machines.Where(m => m.Enabled).ToList();
            if (enabled.Count > 0)
                await Task.WhenAll(enabled.Select(PollMachine));

            try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollMachine(MachineDefinition machine)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, machine.Url + "/api/servers");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", machine.Token);
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) { SetOffline(machine.Id); return; }

            var json    = await resp.Content.ReadAsStringAsync();
            var servers = JsonConvert.DeserializeObject<List<RemoteServerInfo>>(json) ?? new();
            _onlineStatus[machine.Id] = true;
            MachineUpdated?.Invoke(machine.Id, servers);
        }
        catch
        {
            SetOffline(machine.Id);
        }
    }

    private void SetOffline(string id)
    {
        _onlineStatus[id] = false;
        MachineUpdated?.Invoke(id, []);
    }

    private async Task<bool> PostAction(string machineId, string serverId, string action)
    {
        if (!_byId.TryGetValue(machineId, out var machine)) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"{machine.Url}/api/servers/{serverId}/{action}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", machine.Token);
            req.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            return (await _http.SendAsync(req)).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public Task<bool> StartServer(string machineId, string serverId)   => PostAction(machineId, serverId, "start");
    public Task<bool> StopServer(string machineId, string serverId)    => PostAction(machineId, serverId, "stop");
    public Task<bool> RestartServer(string machineId, string serverId) => PostAction(machineId, serverId, "restart");

    public void Dispose()
    {
        _cts?.Cancel();
        _http.Dispose();
    }
}
