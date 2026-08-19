using System.Text.Json;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 请求日志与监控统计服务接口
/// </summary>
public interface ILogService
{
    void AddLog(ProxyLogEntry entry);
    List<ProxyLogEntry> GetRecentLogs(int limit = 100);
    void ClearLogs();
    DashboardSummary GetDashboardSummary(int totalChannels, int activeChannels, string? primaryChannelName);
    LogSettings GetSettings();
    void SaveSettings(LogSettings settings);
}

/// <summary>
/// 支持磁盘持久化与自动定时清理的日志服务实现
/// </summary>
public class LogService : ILogService
{
    private readonly ILogger<LogService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly string _logsPath;
    private readonly string _settingsPath;
    private readonly List<ProxyLogEntry> _logs = new();
    private readonly object _lock = new();
    private bool _isDirty = false;
    private LogSettings _settings = new();
    private readonly System.Threading.Timer _flushTimer;

    private long _totalRequests = 0;
    private long _totalFailovers = 0;
    private long _successfulRequests = 0;
    private long _failedRequests = 0;

    public LogService(ILogger<LogService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;

        var dataDir = Path.Combine(_env.ContentRootPath, "data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        _logsPath = Path.Combine(dataDir, "request_logs.json");
        _settingsPath = Path.Combine(dataDir, "log_settings.json");

        LoadSettings();
        LoadLogs();

        // 定时执行：脏数据落盘与自动过期清理（每 3 秒检查一次）
        _flushTimer = new System.Threading.Timer(_ =>
        {
            lock (_lock)
            {
                if (_settings.AutoCleanupEnabled)
                {
                    PerformCleanupUnderLock();
                }

                if (_isDirty)
                {
                    SaveLogsUnderLock();
                }
            }
        }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    public void AddLog(ProxyLogEntry entry)
    {
        lock (_lock)
        {
            _totalRequests++;
            if (entry.IsFailover)
            {
                _totalFailovers++;
            }
            if (entry.StatusCode >= 200 && entry.StatusCode < 400)
            {
                _successfulRequests++;
            }
            else
            {
                _failedRequests++;
            }

            _logs.Add(entry);
            _isDirty = true;

            // 超限快速截断
            if (_logs.Count > _settings.MaxCapacity * 1.5)
            {
                PerformCleanupUnderLock();
            }
        }
    }

    public List<ProxyLogEntry> GetRecentLogs(int limit = 100)
    {
        lock (_lock)
        {
            return _logs
                .OrderByDescending(x => x.Timestamp)
                .Take(limit)
                .ToList();
        }
    }

    public void ClearLogs()
    {
        lock (_lock)
        {
            _logs.Clear();
            _totalRequests = 0;
            _totalFailovers = 0;
            _successfulRequests = 0;
            _failedRequests = 0;
            _isDirty = true;
            SaveLogsUnderLock();
        }
        _logger.LogInformation("已手动清空所有请求日志与持久化文件");
    }

    public DashboardSummary GetDashboardSummary(int totalChannels, int activeChannels, string? primaryChannelName)
    {
        lock (_lock)
        {
            return new DashboardSummary
            {
                TotalChannels = totalChannels,
                ActiveChannels = activeChannels,
                TotalRequests = _totalRequests,
                TotalFailovers = _totalFailovers,
                SuccessfulRequests = _successfulRequests,
                FailedRequests = _failedRequests,
                CurrentPrimaryChannelName = primaryChannelName
            };
        }
    }

    public LogSettings GetSettings()
    {
        lock (_lock)
        {
            return new LogSettings
            {
                RetentionDays = _settings.RetentionDays,
                MaxCapacity = _settings.MaxCapacity,
                AutoCleanupEnabled = _settings.AutoCleanupEnabled
            };
        }
    }

    public void SaveSettings(LogSettings settings)
    {
        lock (_lock)
        {
            _settings = settings;
            SaveSettingsUnderLock();
            PerformCleanupUnderLock();
            _isDirty = true;
            SaveLogsUnderLock();
        }
        _logger.LogInformation("已更新日志自动清理策略: 保留 {Days} 天, 最多 {Cap} 条", settings.RetentionDays, settings.MaxCapacity);
    }

    private void PerformCleanupUnderLock()
    {
        var originalCount = _logs.Count;

        // 1. 按保留天数清理
        if (_settings.RetentionDays > 0)
        {
            var threshold = DateTime.UtcNow.AddDays(-_settings.RetentionDays);
            _logs.RemoveAll(x => x.Timestamp < threshold);
        }

        // 2. 按最大容量截断（保留最新）
        if (_logs.Count > _settings.MaxCapacity)
        {
            var removeCount = _logs.Count - _settings.MaxCapacity;
            _logs.RemoveRange(0, removeCount);
        }

        if (_logs.Count != originalCount)
        {
            _isDirty = true;
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var s = JsonSerializer.Deserialize<LogSettings>(json);
                if (s != null)
                {
                    _settings = s;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 log_settings.json 失败，使用默认配置");
        }
    }

    private void SaveSettingsUnderLock()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 log_settings.json 失败");
        }
    }

    private void LoadLogs()
    {
        try
        {
            if (File.Exists(_logsPath))
            {
                var json = File.ReadAllText(_logsPath);
                var items = JsonSerializer.Deserialize<List<ProxyLogEntry>>(json);
                if (items != null)
                {
                    lock (_lock)
                    {
                        _logs.Clear();
                        _logs.AddRange(items);
                        _totalRequests = items.Count;
                        _totalFailovers = items.Count(x => x.IsFailover);
                        _successfulRequests = items.Count(x => x.StatusCode >= 200 && x.StatusCode < 400);
                        _failedRequests = items.Count(x => x.StatusCode < 200 || x.StatusCode >= 400);
                    }
                    _logger.LogInformation("已从磁盘恢复 {Count} 条历史请求日志", items.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 request_logs.json 失败");
        }
    }

    private void SaveLogsUnderLock()
    {
        try
        {
            _isDirty = false;
            var options = new JsonSerializerOptions { WriteIndented = false };
            var json = JsonSerializer.Serialize(_logs, options);
            File.WriteAllText(_logsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 request_logs.json 失败");
        }
    }
}
