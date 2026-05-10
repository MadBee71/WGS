namespace WGS.Services;

public record PerfSnapshot(DateTime Time, double Cpu, long MemMb);

public class PerfHistoryService
{
    private readonly Dictionary<string, Queue<PerfSnapshot>> _history = new();
    private const int MaxSamples = 180; // 6 minutes at 2-second intervals

    public void Record(string serverId, double cpu, long memMb)
    {
        if (!_history.TryGetValue(serverId, out var q))
            _history[serverId] = q = new Queue<PerfSnapshot>(MaxSamples + 1);

        q.Enqueue(new PerfSnapshot(DateTime.Now, cpu, memMb));
        while (q.Count > MaxSamples) q.Dequeue();
    }

    public IReadOnlyList<PerfSnapshot> Get(string serverId)
    {
        if (_history.TryGetValue(serverId, out var q))
            return q.ToList();
        return [];
    }

    public void Clear(string serverId) => _history.Remove(serverId);
}
