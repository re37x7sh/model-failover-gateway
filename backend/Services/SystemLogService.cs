using System.Collections.Concurrent;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 系统级运行日志条目实体
/// </summary>
public class SystemLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "INFO";
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}

/// <summary>
/// 系统级日志服务接口
/// </summary>
public interface ISystemLogService
{
    void AddLog(string level, string category, string message, Exception? exception = null);
    List<SystemLogEntry> GetRecentLogs(int limit = 200, string? minLevel = null);
    void ClearLogs();
}

/// <summary>
/// 内存环形系统日志服务实现
/// </summary>
public class SystemLogService : ISystemLogService
{
    private readonly ConcurrentQueue<SystemLogEntry> _logQueue = new();
    private const int MAX_LOG_CAPACITY = 500;

    public void AddLog(string level, string category, string message, Exception? exception = null)
    {
        var entry = new SystemLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level.ToUpperInvariant(),
            Category = category,
            Message = message,
            Exception = exception?.ToString()
        };

        _logQueue.Enqueue(entry);

        // NOTE: 保持最多 500 条日志，避免长期运行消耗内存
        while (_logQueue.Count > MAX_LOG_CAPACITY)
        {
            _logQueue.TryDequeue(out _);
        }
    }

    public List<SystemLogEntry> GetRecentLogs(int limit = 200, string? minLevel = null)
    {
        var query = _logQueue.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(minLevel) && !minLevel.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.Level.Equals(minLevel, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToList();
    }

    public void ClearLogs()
    {
        while (_logQueue.TryDequeue(out _)) { }
    }
}

/// <summary>
/// 将 ASP.NET Core 全局 ILogger 输出重定向至 SystemLogService
/// </summary>
public class SystemLogProvider : ILoggerProvider
{
    private readonly ISystemLogService _systemLogService;

    public SystemLogProvider(ISystemLogService systemLogService)
    {
        _systemLogService = systemLogService;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new MemoryLogger(categoryName, _systemLogService);
    }

    public void Dispose() { }

    private class MemoryLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ISystemLogService _logService;

        public MemoryLogger(string categoryName, ISystemLogService logService)
        {
            _categoryName = categoryName;
            _logService = logService;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var levelStr = logLevel switch
            {
                LogLevel.Trace => "DEBUG",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "FATAL",
                _ => "INFO"
            };

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception == null) return;

            // 简化 Category 名称，提取类名
            var shortCategory = _categoryName;
            var lastDot = _categoryName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < _categoryName.Length - 1)
            {
                shortCategory = _categoryName[(lastDot + 1)..];
            }

            _logService.AddLog(levelStr, shortCategory, message, exception);
        }
    }
}
