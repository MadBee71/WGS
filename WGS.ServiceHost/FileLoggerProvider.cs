using System.Text;
using Microsoft.Extensions.Logging;

namespace WGS.ServiceHost;

/// <summary>
/// Minimal file logger so the service leaves a readable trail next to servers.json (WGS_Data\service.log).
/// The Windows Event Log only keeps Warning and above by default, which hides e.g. which servers were queued for auto-start.
/// Rotates to service.log.old once the file passes ~2 MB. Never throws — logging must not take the service down.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private readonly string _path;
    private readonly object _lock = new();

    public FileLoggerProvider(string path)
    {
        _path = path;
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); } catch { }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() { }

    internal void Write(string category, LogLevel level, string message, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ')
              .Append(level.ToString().ToUpperInvariant().PadRight(11)).Append(' ')
              .Append(category.Split('.').Last()).Append(": ").Append(message);
            if (ex != null) sb.AppendLine().Append(ex);
            sb.AppendLine();

            lock (_lock)
            {
                try
                {
                    var fi = new FileInfo(_path);
                    if (fi.Exists && fi.Length > MaxBytes) File.Move(_path, _path + ".old", true);
                }
                catch { }
                File.AppendAllText(_path, sb.ToString());
            }
        }
        catch { }
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            owner.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
