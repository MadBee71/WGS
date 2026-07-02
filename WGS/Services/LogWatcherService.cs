using System.Collections.Concurrent;
using WGS.Models;

namespace WGS.Services;

public class LogWatcherService
{
    private readonly ServerManagerService _manager;
    private readonly NotificationService  _notifications;

    // serverId -> (keyword -> last trigger time)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DateTime>> _lastFired = new();

    public LogWatcherService(ServerManagerService manager, NotificationService notifications)
    {
        _manager       = manager;
        _notifications = notifications;
    }

    public void CheckLine(GameServer server, string line)
    {
        if (string.IsNullOrEmpty(line) || server.LogWatchRules.Count == 0) return;

        var cooldowns = _lastFired.GetOrAdd(server.Id, _ => new());
        var now = DateTime.Now;

        foreach (var rule in server.LogWatchRules)
        {
            if (!rule.Enabled) continue;
            if (string.IsNullOrWhiteSpace(rule.Keyword)) continue;
            if (!line.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase)) continue;

            var key = rule.Keyword.ToLowerInvariant();
            if (cooldowns.TryGetValue(key, out var last) &&
                now - last < TimeSpan.FromMinutes(Math.Max(rule.CooldownMin, 0)))
                continue;

            cooldowns[key] = now;
            _ = TriggerAsync(server, rule, line);
        }
    }

    private async Task TriggerAsync(GameServer server, LogWatchRule rule, string line)
    {
        var label = $"[LogWatch] Keyword \"{rule.Keyword}\" matched in {server.DisplayName}: {line.Trim()}";

        switch (rule.Action)
        {
            case LogWatchAction.Restart:
                await _notifications.NotifyAsync($"⚠ Log Watch — {server.DisplayName}",
                    $"Restarting server. Keyword: `{rule.Keyword}`\nLine: `{line.Trim()}`", "#D29922");
                await _manager.StopAsync(server);
                await Task.Delay(3000);
                await _manager.StartAsync(server);
                break;

            case LogWatchAction.Stop:
                await _notifications.NotifyAsync($"⚠ Log Watch — {server.DisplayName}",
                    $"Stopping server. Keyword: `{rule.Keyword}`\nLine: `{line.Trim()}`", "#DA3633");
                await _manager.StopAsync(server);
                break;

            case LogWatchAction.SendRcon:
                if (!string.IsNullOrWhiteSpace(rule.RconCommand))
                    await _manager.SendCommandAsync(server.Id, rule.RconCommand);
                break;

            case LogWatchAction.Notify:
                await _notifications.NotifyAsync($"ℹ Log Watch — {server.DisplayName}",
                    $"Keyword: `{rule.Keyword}`\nLine: `{line.Trim()}`", "#58A6FF");
                break;
        }

        // Log to server console
        _manager.InjectLogLine(server.Id, $"[LogWatch] Action {rule.Action} triggered by keyword \"{rule.Keyword}\"",
            ConsoleMessageType.System);
    }
}
