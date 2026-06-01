using System.Collections.Concurrent;
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
    private readonly object _machinesLock = new();
    private readonly List<MachineDefinition> _machines = new();
    private readonly Dictionary<string, MachineDefinition> _byId = new();
    // ConcurrentDictionary: written by multiple parallel PollMachine tasks
    private readonly ConcurrentDictionary<string, bool> _onlineStatus = new();
    private readonly HttpClient _http;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public event Action<string, List<RemoteServerInfo>>? MachineUpdated;
    public event Action? MachinesChanged;

    public IReadOnlyList<MachineDefinition> Machines
    {
        get { lock (_machinesLock) return _machines.ToList(); }
    }

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
            lock (_machinesLock)
                foreach (var m in list) RegisterLocked(m);
        }
        catch { }
    }

    private void Save()
    {
        List<MachineDefinition> snapshot;
        lock (_machinesLock) snapshot = _machines.ToList();
        File.WriteAllText(_machinesFile, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
    }

    // Must be called inside _machinesLock
    private void RegisterLocked(MachineDefinition def)
    {
        def.Url = def.Url.TrimEnd('/');
        _machines.Add(def);
        _byId[def.Id] = def;
    }

    public void AddMachine(MachineDefinition def)
    {
        lock (_machinesLock) RegisterLocked(def);
        Save();
        MachinesChanged?.Invoke();
    }

    public void RemoveMachine(string id)
    {
        lock (_machinesLock)
        {
            _machines.RemoveAll(m => m.Id == id);
            _byId.Remove(id);
        }
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
            List<MachineDefinition> enabled;
            lock (_machinesLock) enabled = _machines.Where(m => m.Enabled).ToList();

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
        MachineDefinition? machine;
        lock (_machinesLock) _byId.TryGetValue(machineId, out machine);
        if (machine == null) return false;
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
        // Wait for poll loop to finish before disposing HttpClient
        try { _pollTask?.Wait(3000); } catch { }
        _http.Dispose();
    }
}
