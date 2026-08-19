using WGS.Models;

namespace WGS.Games;

/// <summary>
/// Optional interface for rich plugins that need safe access to WGS host operations.
/// WGS initializes the context for each server detail/session before plugin config/UI hooks run.
/// </summary>
public interface IGameHostContextPlugin
{
    void InitializeHost(GamePluginContext context);
}

/// <summary>
/// Stable, game-agnostic host operations exposed to rich plugins.
/// Plugins should use this instead of reaching into App.Services or concrete WGS services.
/// </summary>
public sealed class GamePluginContext
{
    private readonly Func<bool> _isRunning;
    private readonly Func<Task> _start;
    private readonly Func<Task> _stop;
    private readonly Func<Task> _kill;
    private readonly Func<Task<GamePluginBackupInfo>> _createBackup;
    private readonly Func<IReadOnlyList<GamePluginBackupInfo>> _getBackups;
    private readonly Func<string, Task> _sendCommand;
    private readonly Func<string, Task<bool>> _sendCommandNoWait;
    private readonly Action<string, GamePluginLogLevel> _log;
    private readonly Action<string> _openPath;

    internal GamePluginContext(
        GameServer server,
        Func<bool> isRunning,
        Func<Task> start,
        Func<Task> stop,
        Func<Task> kill,
        Func<Task<GamePluginBackupInfo>> createBackup,
        Func<IReadOnlyList<GamePluginBackupInfo>> getBackups,
        Func<string, Task> sendCommand,
        Func<string, Task<bool>> sendCommandNoWait,
        Action<string, GamePluginLogLevel> log,
        Action<string> openPath)
    {
        Server = server;
        _isRunning = isRunning;
        _start = start;
        _stop = stop;
        _kill = kill;
        _createBackup = createBackup;
        _getBackups = getBackups;
        _sendCommand = sendCommand;
        _sendCommandNoWait = sendCommandNoWait;
        _log = log;
        _openPath = openPath;
    }

    public GameServer Server { get; }

    public bool IsRunning => _isRunning();

    public string InstallPath => Server.InstallPath;

    public Task StartServerAsync() => _start();
    public Task StopServerAsync() => _stop();
    public Task KillServerAsync() => _kill();

    public Task<GamePluginBackupInfo> CreateBackupAsync() => _createBackup();
    public IReadOnlyList<GamePluginBackupInfo> GetBackups() => _getBackups();

    /// <summary>
    /// Sends a command through the active WGS RCON connection when available,
    /// otherwise through the server's normal console/command transport.
    /// </summary>
    public Task SendCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return Task.CompletedTask;

        return _sendCommand(command);
    }

    /// <summary>
    /// Sends a command without waiting for a response when the active remote-command
    /// transport supports it. Returns false when no no-wait transport is available.
    /// Plugins may then choose whether to fall back to SendCommandAsync().
    /// </summary>
    public Task<bool> SendCommandNoWaitAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return Task.FromResult(false);

        return _sendCommandNoWait(command);
    }

    public void Log(string message, GamePluginLogLevel level = GamePluginLogLevel.Info)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _log(message, level);
    }

    public void OpenInstallFolder() => OpenPath(InstallPath);

    public void OpenPath(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            _openPath(path);
    }
}

public sealed class GamePluginBackupInfo
{
    public string FilePath { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
    public bool IsIncremental { get; init; }

    public string SizeText => SizeBytes > 1_000_000
        ? $"{SizeBytes / 1_000_000.0:F1} MB"
        : $"{SizeBytes / 1_000.0:F0} KB";
}

public enum GamePluginLogLevel
{
    Info,
    Warning,
    Error
}
