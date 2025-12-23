namespace FolderIconManager.Core.Services;

/// <summary>
/// Simple logging service for tracking operations
/// </summary>
public class LogService
{
    private readonly List<LogEntry> _entries = [];
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when a new log entry is added
    /// </summary>
    public event Action<LogEntry>? OnLog;

    /// <summary>
    /// All log entries
    /// </summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public void Info(string message) => Log(LogLevel.Info, message);
    public void Success(string message) => Log(LogLevel.Success, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
    public void Debug(string message) => Log(LogLevel.Debug, message);

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Exception = exception
        };

        lock (_lock)
        {
            _entries.Add(entry);
        }

        OnLog?.Invoke(entry);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}

/// <summary>
/// A single log entry
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public required string Message { get; init; }
    public Exception? Exception { get; init; }

    public override string ToString()
    {
        var time = Timestamp.ToString("HH:mm:ss");
        var prefix = Level switch
        {
            LogLevel.Debug => "[DBG]",
            LogLevel.Info => "[INF]",
            LogLevel.Success => "[OK ]",
            LogLevel.Warning => "[WRN]",
            LogLevel.Error => "[ERR]",
            _ => "[???]"
        };
        
        var msg = $"{time} {prefix} {Message}";
        if (Exception != null)
        {
            msg += $"\n         {Exception.Message}";
        }
        return msg;
    }
}

/// <summary>
/// Log severity levels
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Success,
    Warning,
    Error
}

