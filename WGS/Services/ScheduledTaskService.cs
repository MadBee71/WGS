using Newtonsoft.Json;

namespace WGS.Services;

public enum ScheduledActionType { Restart, Stop, Start, Backup, Update }
public enum ScheduleFrequency   { Once, Daily, Weekly }

public class ScheduledTask
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string ServerId    { get; set; } = string.Empty;
    public string ServerName  { get; set; } = string.Empty;
    public ScheduledActionType Action    { get; set; }
    public ScheduleFrequency   Frequency { get; set; }
    public TimeSpan            TimeOfDay { get; set; }
    public DayOfWeek           DayOfWeek { get; set; }
    public bool   IsEnabled   { get; set; } = true;
    public DateTime? LastRun  { get; set; }
    public DateTime? NextRun  { get; set; }

    public string ActionText => Action switch
    {
        ScheduledActionType.Restart => "Restart",
        ScheduledActionType.Stop    => "Stop",
        ScheduledActionType.Start   => "Start",
        ScheduledActionType.Backup  => "Backup",
        ScheduledActionType.Update  => "Update",
        _ => Action.ToString()
    };

    public string FrequencyText => Frequency switch
    {
        ScheduleFrequency.Daily  => $"Daily {TimeOfDay:hh\\:mm}",
        ScheduleFrequency.Weekly => $"{DayOfWeek} {TimeOfDay:hh\\:mm}",
        ScheduleFrequency.Once   => $"Once {NextRun:dd.MM HH:mm}",
        _ => Frequency.ToString()
    };
}

public class ScheduledTaskService : IDisposable
{
    private readonly ConfigService _config;
    private readonly ServerManagerService _manager;
    private readonly BackupService _backup;
    private readonly System.Timers.Timer _timer;
    private List<ScheduledTask> _tasks = [];
    private readonly string _file;

    public event Action<ScheduledTask, string>? TaskExecuted;

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

    public IReadOnlyList<ScheduledTask> Tasks => _tasks;

    public void AddTask(ScheduledTask task)
    {
        task.NextRun = ComputeNextRun(task);
        _tasks.Add(task);
        Save();
    }

    public void RemoveTask(string id)
    {
        _tasks.RemoveAll(t => t.Id == id);
        Save();
    }

    public void UpdateTask(ScheduledTask task)
    {
        var idx = _tasks.FindIndex(t => t.Id == task.Id);
        if (idx >= 0) { _tasks[idx] = task; Save(); }
    }

    private async Task CheckTasksAsync()
    {
        var now = DateTime.Now;
        foreach (var task in _tasks.Where(t => t.IsEnabled && t.NextRun <= now))
        {
            await ExecuteTaskAsync(task);
            task.LastRun = now;
            task.NextRun = task.Frequency == ScheduleFrequency.Once
                ? null
                : ComputeNextRun(task);
            if (task.NextRun == null) task.IsEnabled = false;
        }
        Save();
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
                    await _manager.StopAsync(server);
                    await Task.Delay(3000);
                    await _manager.StartAsync(server);
                    break;
                case ScheduledActionType.Stop:
                    await _manager.StopAsync(server);
                    break;
                case ScheduledActionType.Start:
                    await _manager.StartAsync(server);
                    break;
                case ScheduledActionType.Backup:
                    await _backup.CreateBackupAsync(server);
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
        => _config.LoadServers().FirstOrDefault(s => s.Id == id);

    private static DateTime ComputeNextRun(ScheduledTask task)
    {
        var now  = DateTime.Now;
        var today = now.Date + task.TimeOfDay;

        return task.Frequency switch
        {
            ScheduleFrequency.Daily  => today > now ? today : today.AddDays(1),
            ScheduleFrequency.Weekly =>
                Enumerable.Range(0, 8)
                    .Select(d => today.AddDays(d))
                    .First(d => d.DayOfWeek == task.DayOfWeek && d > now),
            _ => task.NextRun ?? now.AddMinutes(1),
        };
    }

    private void Save()
    {
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
