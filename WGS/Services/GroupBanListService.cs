using System.IO;
using Newtonsoft.Json;

namespace WGS.Services;

public class GroupBanEntry
{
    public string   GroupId  { get; set; } = string.Empty;
    public string   GameId   { get; set; } = string.Empty;
    public string   Target   { get; set; } = string.Empty; // SteamId or player name, whatever the ban command used
    public string   Reason   { get; set; } = string.Empty;
    public DateTime BannedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Centralized ban list per server group. When a player is banned on one server, the same
/// ban is recorded here and replayed on the group's other servers of the same game — both
/// immediately (if they're running) and again on their next start (in case they weren't).
/// </summary>
public class GroupBanListService
{
    private readonly string _file;
    private List<GroupBanEntry> _entries = [];
    private readonly object _lock = new();

    public GroupBanListService(ConfigService config)
    {
        _file = Path.Combine(config.AppDataPath, "group_bans.json");
        Load();
    }

    public void AddBan(string groupId, string gameId, string target, string reason)
    {
        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(target)) return;
        lock (_lock)
        {
            // Avoid duplicate entries for the same player in the same group/game
            _entries.RemoveAll(e => e.GroupId == groupId && e.GameId == gameId
                                  && e.Target.Equals(target, StringComparison.OrdinalIgnoreCase));
            _entries.Add(new GroupBanEntry { GroupId = groupId, GameId = gameId, Target = target, Reason = reason });
            Save();
        }
    }

    public List<GroupBanEntry> GetBans(string groupId, string gameId)
    {
        lock (_lock)
            return _entries.Where(e => e.GroupId == groupId && e.GameId == gameId).ToList();
    }

    private void Load()
    {
        if (!File.Exists(_file)) return;
        try { _entries = JsonConvert.DeserializeObject<List<GroupBanEntry>>(File.ReadAllText(_file)) ?? []; }
        catch { _entries = []; }
    }

    private void Save()
    {
        try { File.WriteAllText(_file, JsonConvert.SerializeObject(_entries, Formatting.Indented)); }
        catch { }
    }
}
