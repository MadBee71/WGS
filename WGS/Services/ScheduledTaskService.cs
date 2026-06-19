using Newtonsoft.Json;

namespace WGS.Services;

public enum ScheduledActionType { Restart, Stop, Start, Backup, Update, QuickCommand }
public enum ScheduleFrequency   { Once, Daily, Weekly, Interval }

public class ScheduledTask
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string ServerId    { get; set; } = string.Empty;
    public string ServerName  { get; set; } = string.Empty;
    public ScheduledActionType Action    { get; set; }
    public ScheduleFrequency   Frequency { get; set; }
    public TimeSpan            TimeOfDay { get; set; }
    public DayOfWeek           DayOfWeek { get; set; }
    /// <summary>Used when Frequency == Interval — repeat every N minutes.</summary>
    public int    IntervalMinutes { get; set; } = 60;
    /// <summary>Console command to send when Action == QuickCommand.</summary>
    public string Command     { get; set; } = string.Empty;
    public bool   IsEnabled   { get; set; } = true;
    public DateTime? LastRun  { get; set; }
    public DateTime? NextRun  { get; set; }

    public string ActionText => Action switch
    {
        ScheduledActionType.Restart      => "Restart",
        ScheduledActionType.Stop         => "Stop",
        ScheduledActionType.Start        => "Start",
        ScheduledActionType.Backup       => "Backup",
        ScheduledActionType.Update       => "Update",
        ScheduledActionType.QuickCommand => $"Command: {Command}",
        _ => Action.ToString()
    };

    public string FrequencyText => Frequency switch
    {
        ScheduleFrequency.Daily    => $"Daily {TimeOfDay:hh\\:mm}",
        ScheduleFrequency.Weekly   => $"{DayOfWeek} {TimeOfDay:hh\\:mm}",
        ScheduleFrequency.Once     => $"Once {NextRun:dd.MM HH:mm}",
        ScheduleFrequency.Interval => IntervalMinutes % 60 == 0
            ? $"Every {IntervalMinutes / 60}h"
            : $"Every {IntervalMinutes}min",
        _ => Frequency.ToString()
    };
}

public class ScheduledTaskService : IDisposable
{
    private readonly ConfigService _config;
    private readonly ServerManagerService _manager;
    private readonly BackupService _backup;
    private readonly System.Timers.Timer _timer;
    private readonly object _lock = new();
    private List<ScheduledTask> _tasks = [];
    private readonly string _file;

    public event Action<ScheduledTask, string>? TaskExecuted;

    /// <summary>Wired by MainViewModel to return live in-memory server objects.</summary>
    public Func<IEnumerable<Models.GameServer>>? GetServers { get; set; }

    /// <summary>Wired by MainViewModel to trigger a server update via its ViewModel command.</summary>
    public Func<string, Task>? UpdateServer { get; set; }

    public ScheduledTaskService(ConfigService config, ServerManagerService manager, BackupService backup)
    {
        _config  = config;
        _manager = manager;
        _backup  = backup;
        _file    = System.IO.Path.Combine(config.AppDataPath, "scheduled_tasks.json");

        Load();
        _timer = new System.Timers.Timer(30_000); // check every 30s
        _timer.Elapsed += async (_, _) => await CheckTasksAsync();
        _timer.Start();
    }

    public IReadOnlyList<ScheduledTask> Tasks
    {
        get { lock (_lock) { return _tasks.ToList(); } }
    }

    public void AddTask(ScheduledTask task)
    {
        task.NextRun = ComputeNextRun(task);
        lock (_lock) { _tasks.Add(task); Save(); }
    }

    public void RemoveTask(string id)
    {
        lock (_lock) { _tasks.RemoveAll(t => t.Id == id); Save(); }
    }

    public void UpdateTask(ScheduledTask task)
    {
        lock (_lock)
        {
            var idx = _tasks.FindIndex(t => t.Id == task.Id);
            if (idx >= 0) { _tasks[idx] = task; Save(); }
        }
    }

    private async Task CheckTasksAsync()
    {
        var now = DateTime.Now;
        List<ScheduledTask> due;
        lock (_lock)
        {
            due = _tasks.Where(t => t.IsEnabled && t.NextRun <= now).ToList();
        }

        if (due.Count == 0) return;

        // Run concurrently — a Restart task now waits ~60s to warn players first, and
        // that must not delay other due tasks (e.g. other servers' restarts/backups).
        await Task.WhenAll(due.Select(ExecuteTaskAsync));

        lock (_lock)
        {
            foreach (var task in due)
            {
                task.LastRun = now;
                task.NextRun = task.Frequency == ScheduleFrequency.Once
                    ? null
                    : ComputeNextRun(task);
                if (task.NextRun == null) task.IsEnabled = false;
            }
            Save();
        }
    }

    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        var server = GetServer(task.ServerId);
        if (server == null) return;

        try
        {
            switch (task.Action)
            {
                case ScheduledActionType.Restart:
                    await _manager.WarnPlayersAsync(server, "Server restarting in 1 minute");
                    await Task.Delay(60_000);
                    await _manager.StopAsync(server);
                    if (server.BackupOnShutdown) { try { await _backup.CreateBackupAsync(server); } catch { } }
                    await Task.Delay(3000);
                    await _manager.StartAsync(server);
                    break;
                case ScheduledActionType.Stop:
                    await _manager.StopAsync(server);
                    if (server.BackupOnShutdown) { try { await _backup.CreateBackupAsync(server); } catch { } }
                    break;
                case ScheduledActionType.Start:
                    await _manager.StartAsync(server);
                    break;
                case ScheduledActionType.Backup:
                    if (!_manager.IsRunning(server.Id) &&
                        (server.LastStarted == null || server.LastStarted < DateTime.Now.AddHours(-24)))
                    {
                        TaskExecuted?.Invoke(task, "Skipped — server hasn't run in the last 24h, nothing new to back up");
                        return;
                    }
                    await _backup.CreateBackupAsync(server);
                    break;
                case ScheduledActionType.Update:
                    if (UpdateServer != null) await UpdateServer(task.ServerId);
                    break;
                case ScheduledActionType.QuickCommand:
                    if (!string.IsNullOrWhiteSpace(task.Command))
                        await _manager.SendCommandAsync(server.Id, task.Command);
                    break;
            }
            TaskExecuted?.Invoke(task, "OK");
        }
        catch (Exception ex)
        {
            TaskExecuted?.Invoke(task, ex.Message);
        }
    }

    private Models.GameServer? GetServer(string id)
        => GetServers?.Invoke().FirstOrDefault(s => s.Id == id)
           ?? _config.LoadServers().FirstOrDefault(s => s.Id == id);

    private static DateTime ComputeNextRun(ScheduledTask task)
    {
        var now   = DateTime.Now;
        var today = now.Date + task.TimeOfDay;

        return task.Frequency switch
        {
            ScheduleFrequency.Daily    => today > now ? today : today.AddDays(1),
            ScheduleFrequency.Weekly   =>
                Enumerable.Range(0, 8)
                    .Select(d => today.AddDays(d))
                    .First(d => d.DayOfWeek == task.DayOfWeek && d > now),
            ScheduleFrequency.Interval => (task.LastRun ?? now).AddMinutes(Math.Max(1, task.IntervalMinutes)),
            _ => task.NextRun ?? now.AddMinutes(1),
        };
    }

    private void Save()
    {
        // Caller must hold _lock
        System.IO.File.WriteAllText(_file, JsonConvert.SerializeObject(_tasks, Formatting.Indented));
    }

    private void Load()
    {
        if (!System.IO.File.Exists(_file)) return;
        try { _tasks = JsonConvert.DeserializeObject<List<ScheduledTask>>(System.IO.File.ReadAllText(_file)) ?? []; }
        catch { }
    }

    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
