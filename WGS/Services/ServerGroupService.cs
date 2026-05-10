using Newtonsoft.Json;

namespace WGS.Services;

public class ServerGroup
{
    public string Id      { get; set; } = Guid.NewGuid().ToString();
    public string Name    { get; set; } = string.Empty;
    public string Color   { get; set; } = "#58A6FF";
    public List<string> ServerIds { get; set; } = [];
}

public class ServerGroupService
{
    private readonly string _file;
    private List<ServerGroup> _groups = [];

    public event Action? Changed;

    public ServerGroupService(ConfigService config)
    {
        _file = System.IO.Path.Combine(config.AppDataPath, "server_groups.json");
        Load();
    }

    public IReadOnlyList<ServerGroup> Groups => _groups;

    public void Add(ServerGroup g)    { _groups.Add(g); Save(); Changed?.Invoke(); }
    public void Remove(string id)     { _groups.RemoveAll(g => g.Id == id); Save(); Changed?.Invoke(); }
    public void Update(ServerGroup g) { var i = _groups.FindIndex(x => x.Id == g.Id); if (i >= 0) { _groups[i] = g; Save(); Changed?.Invoke(); } }

    public ServerGroup? GetForServer(string serverId)
        => _groups.FirstOrDefault(g => g.ServerIds.Contains(serverId));

    public void SetServerGroup(string serverId, string? groupId)
    {
        foreach (var g in _groups) g.ServerIds.Remove(serverId);
        if (groupId != null)
        {
            var g = _groups.FirstOrDefault(x => x.Id == groupId);
            g?.ServerIds.Add(serverId);
        }
        Save(); Changed?.Invoke();
    }

    private void Save()
        => System.IO.File.WriteAllText(_file, JsonConvert.SerializeObject(_groups, Formatting.Indented));

    private void Load()
    {
        if (!System.IO.File.Exists(_file)) return;
        try { _groups = JsonConvert.DeserializeObject<List<ServerGroup>>(_file.Length > 0
            ? System.IO.File.ReadAllText(_file) : "[]") ?? []; }
        catch { }
    }
}
