using Newtonsoft.Json;

namespace WGS.Services;

public enum ScheduledActionType { Restart, Stop, Start, Backup, Update, QuickCommand, Wipe, WipeMap, Broadcast }
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
        ScheduledActionType.Wipe         => "Wipe (full)",
        ScheduledActionType.WipeMap      => "Wipe (map only)",
        ScheduledActionType.Broadcast    => $"Broadcast: {Command}",
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
    private readonly NotificationService _notifications;
    private readonly System.Timers.Timer _timer;
    private readonly object _lock = new();
    private List<ScheduledTask> _tasks = [];
    private readonly string _file;
    private int _checkRunning; // Interlocked flag — prevents concurrent CheckTasksAsync runs

    public event Action<ScheduledTask, string>? TaskExecuted;

    /// <summary>Wired by MainViewModel to return live in-memory server objects.</summary>
    public Func<IEnumerable<Models.GameServer>>? GetServers { get; set; }

    /// <summary>Wired by MainViewModel to trigger a server update via its ViewModel command.</summary>
    public Func<string, Task>? UpdateServer { get; set; }

    public ScheduledTaskService(ConfigService config, ServerManagerService manager, BackupService backup,
                                NotificationService notifications)
    {
        _config        = config;
        _manager       = manager;
        _backup        = backup;
        _notifications = notifications;
        _file          = System.IO.Path.Combine(config.AppDataPath, "scheduled_tasks.json");

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
        List<ScheduledTask> snapshot;
        lock (_lock) { _tasks.Add(task); snapshot = [.. _tasks]; }
        SaveSnapshot(snapshot);
    }

    public void RemoveTask(string id)
    {
        List<ScheduledTask> snapshot;
        lock (_lock) { _tasks.RemoveAll(t => t.Id == id); snapshot = [.. _tasks]; }
        SaveSnapshot(snapshot);
    }

    public void UpdateTask(ScheduledTask task)
    {
        List<ScheduledTask>? snapshot = null;
        lock (_lock)
        {
            var idx = _tasks.FindIndex(t => t.Id == task.Id);
            if (idx >= 0) { _tasks[idx] = task; snapshot = [.. _tasks]; }
        }
        if (snapshot != null) SaveSnapshot(snapshot);
    }

    public async Task ExecuteNowAsync(string taskId)
    {
        ScheduledTask? task;
        lock (_lock) { task = _tasks.FirstOrDefault(t => t.Id == taskId); }
        if (task != null) await ExecuteTaskAsync(task);
    }

    private async Task CheckTasksAsync()
    {
        if (Interlocked.CompareExchange(ref _checkRunning, 1, 0) != 0) return;
        try { await CheckTasksCoreAsync(); }
        finally { Interlocked.Exchange(ref _checkRunning, 0); }
    }

    private async Task CheckTasksCoreAsync()
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

        List<ScheduledTask> snapshot;
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
            snapshot = [.. _tasks];
        }
        SaveSnapshot(snapshot);
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
                case ScheduledActionType.Wipe:
                case ScheduledActionType.WipeMap:
                    if (!await ExecuteWipeAsync(server, task.Action == ScheduledActionType.Wipe))
                    {
                        TaskExecuted?.Invoke(task, "Skipped — this game does not support scheduled wipes");
                        return;
                    }
                    break;
                case ScheduledActionType.Broadcast:
                    if (!string.IsNullOrWhiteSpace(task.Command))
                    {
                        var bPlugin = Games.GameRegistry.All.FirstOrDefault(p => p.GameId == server.GameId);
                        var bcmd    = bPlugin?.GetBroadcastCommand(task.Command);
                        if (bcmd != null)
                            await _manager.SendCommandAsync(server.Id, bcmd);
                        else
                        {
                            TaskExecuted?.Invoke(task, "Skipped — this game does not support in-game broadcasts");
                            return;
                        }
                    }
                    break;
            }
            TaskExecuted?.Invoke(task, "OK");
        }
        catch (Exception ex)
        {
            TaskExecuted?.Invoke(task, ex.Message);
        }
    }

    // Returns false if the game doesn't support wipes (caller reports the skip).
    private async Task<bool> ExecuteWipeAsync(Models.GameServer server, bool fullWipe)
    {
        var plugin = Games.GameRegistry.All.FirstOrDefault(p => p.GameId == server.GameId);
        if (plugin is not Games.IWipePlugin wipePlugin)
            return false;

        // Warn players and stop the server (skip warning if already stopped)
        if (_manager.IsRunning(server.Id))
        {
            await _manager.WarnPlayersAsync(server, "Server wiping in 2 minutes — all world data will be reset");
            await Task.Delay(120_000);
            await _manager.StopAsync(server);
            await Task.Delay(3000);
        }

        // Collect and delete wipe paths (glob-resolved)
        var patterns = fullWipe
            ? wipePlugin.GetFullWipePaths(server)
            : wipePlugin.GetMapWipePaths(server);

        int deleted = 0;
        var errors  = new System.Text.StringBuilder();

        foreach (var pattern in patterns)
        {
            var absPattern = System.IO.Path.Combine(server.InstallPath, pattern);
            var dir        = System.IO.Path.GetDirectoryName(absPattern) ?? server.InstallPath;
            var glob       = System.IO.Path.GetFileName(absPattern);

            if (!System.IO.Directory.Exists(dir)) continue;

            // Delete matching files
            foreach (var file in System.IO.Directory.GetFiles(dir, glob))
            {
                try { System.IO.File.Delete(file); deleted++; }
                catch (Exception ex) { errors.AppendLine(ex.Message); }
            }

            // Delete matching directories (e.g. storage_1/*)
            if (glob == "*")
            {
                foreach (var sub in System.IO.Directory.GetDirectories(dir))
                {
                    try { System.IO.Directory.Delete(sub, recursive: true); deleted++; }
                    catch (Exception ex) { errors.AppendLine(ex.Message); }
                }
            }
        }

        var label = fullWipe ? "Full wipe" : "Map wipe";
        _manager.InjectLogLine(server.Id,
            $"[Wipe] {label} complete — {deleted} item(s) removed" +
            (errors.Length > 0 ? $" (errors: {errors})" : ""),
            Models.ConsoleMessageType.Warning);

        await _notifications.NotifyAsync(
            $"🗑 {label} — {server.DisplayName}",
            $"{label} complete on **{server.DisplayName}** — {deleted} item(s) removed. Restarting now.",
            "#D29922");

        await Task.Delay(2000);
        await _manager.StartAsync(server);
        return true;
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

    private void SaveSnapshot(List<ScheduledTask> snapshot)
        => System.IO.File.WriteAllText(_file, JsonConvert.SerializeObject(snapshot, Formatting.Indented));

    private void Load()
    {
        if (!System.IO.File.Exists(_file)) return;
        try { _tasks = JsonConvert.DeserializeObject<List<ScheduledTask>>(System.IO.File.ReadAllText(_file)) ?? []; }
        catch { }
    }

    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
