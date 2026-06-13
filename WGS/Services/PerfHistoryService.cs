using System.IO;
using System.Text.Json;

namespace WGS.Services;

public record PerfSnapshot(DateTime Time, double Cpu, long MemMb);

public class PerfHistoryService
{
    private readonly Dictionary<string, Queue<PerfSnapshot>> _history = new();
    private readonly object _lock = new();
    private readonly string _dir;

    // 1 sample every ~2 s → 1800 samples ≈ 1 hour of history in memory
    private const int MaxSamples = 1800;

    public PerfHistoryService(ConfigService config)
    {
        _dir = Path.Combine(config.AppDataPath, "perf_history");
        Directory.CreateDirectory(_dir);
    }

    public void Record(string serverId, double cpu, long memMb)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(serverId, out var q))
            {
                q = LoadFromDisk(serverId);
                _history[serverId] = q;
            }

            q.Enqueue(new PerfSnapshot(DateTime.Now, cpu, memMb));
            while (q.Count > MaxSamples) q.Dequeue();

            // persist every 60 samples (~2 min)
            if (q.Count % 60 == 0)
                SaveToDisk(serverId, q);
        }
    }

    public IReadOnlyCollection<PerfSnapshot> Get(string serverId)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(serverId, out var q))
            {
                q = LoadFromDisk(serverId);
                _history[serverId] = q;
            }
            return q.ToList();
        }
    }

    public void Clear(string serverId)
    {
        lock (_lock)
        {
            _history.Remove(serverId);
            var f = FilePath(serverId);
            if (File.Exists(f)) File.Delete(f);
        }
    }

    private string FilePath(string serverId)
        => Path.Combine(_dir, $"{serverId}.json");

    private Queue<PerfSnapshot> LoadFromDisk(string serverId)
    {
        var q = new Queue<PerfSnapshot>(MaxSamples + 1);
        var f = FilePath(serverId);
        if (!File.Exists(f)) return q;
        try
        {
            var list = JsonSerializer.Deserialize<List<PerfSnapshot>>(File.ReadAllText(f));
            if (list != null)
                foreach (var s in list.TakeLast(MaxSamples))
                    q.Enqueue(s);
        }
        catch { /* corrupt → start fresh */ }
        return q;
    }

    private void SaveToDisk(string serverId, Queue<PerfSnapshot> q)
    {
        try { File.WriteAllText(FilePath(serverId), JsonSerializer.Serialize(q.ToList())); }
        catch { }
    }
}
