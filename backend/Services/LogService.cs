using System.Text.Json;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 请求日志与监控统计服务接口
/// </summary>
public interface ILogService
{
    void StartLog(ProxyLogEntry entry);
    void CompleteLog(
        string logId, 
        int statusCode, 
        string status, 
        long durationMs, 
        List<string> triedChannels, 
        string? finalChannel, 
        bool isFailover, 
        string? errorDetails = null, 
        string? responseBody = null, 
        long promptTokens = 0, 
        long completionTokens = 0);
    void AddLog(ProxyLogEntry entry);
    List<ProxyLogEntry> GetRecentLogs(int limit = 100);
    PagedResult<ProxyLogEntry> GetPagedLogs(int page = 1, int pageSize = 50, string? filter = null, string? keyword = null);
    void ClearLogs();
    DashboardSummary GetDashboardSummary(int totalChannels, int activeChannels, string? primaryChannelName);
    LogSettings GetSettings();
    void SaveSettings(LogSettings settings);
    void Flush();
}

/// <summary>
/// 支持磁盘持久化、分页检索与自动定时清理的日志服务实现
/// </summary>
public class LogService : ILogService, IDisposable
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

        // 注册进程退出事件，确保优雅停机时立即落盘
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();

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

    /// <summary>
    /// 请求发起时立即记录（初始状态为 PENDING 运行中）
    /// </summary>
    public void StartLog(ProxyLogEntry entry)
    {
        lock (_lock)
        {
            entry.Status = "PENDING";
            _logs.Add(entry);
            _isDirty = true;

            // 超限快速截断
            if (_logs.Count > _settings.MaxCapacity * 1.5)
            {
                PerformCleanupUnderLock();
            }
        }
    }

    /// <summary>
    /// 请求完成或中断时原子更新运行状态、耗时与响应摘要
    /// </summary>
    public void CompleteLog(
        string logId, 
        int statusCode, 
        string status, 
        long durationMs, 
        List<string> triedChannels, 
        string? finalChannel, 
        bool isFailover, 
        string? errorDetails = null, 
        string? responseBody = null, 
        long promptTokens = 0, 
        long completionTokens = 0)
    {
        lock (_lock)
        {
            _totalRequests++;
            if (isFailover) _totalFailovers++;
            if (status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                _successfulRequests++;
            }
            else
            {
                _failedRequests++;
            }

            var entry = _logs.FirstOrDefault(x => x.Id == logId);
            if (entry != null)
            {
                entry.StatusCode = statusCode;
                entry.Status = status;
                entry.DurationMs = durationMs;
                entry.TriedChannels = triedChannels ?? new();
                entry.FinalChannel = finalChannel;
                entry.IsFailover = isFailover;
                if (!string.IsNullOrWhiteSpace(errorDetails)) entry.ErrorDetails = errorDetails;
                if (!string.IsNullOrWhiteSpace(responseBody)) entry.ResponseBody = responseBody;
                if (promptTokens > 0) entry.PromptTokens = promptTokens;
                if (completionTokens > 0) entry.CompletionTokens = completionTokens;
            }
            else
            {
                // 若找不到则兜底新增
                _logs.Add(new ProxyLogEntry
                {
                    Id = logId,
                    StatusCode = statusCode,
                    Status = status,
                    DurationMs = durationMs,
                    TriedChannels = triedChannels ?? new(),
                    FinalChannel = finalChannel,
                    IsFailover = isFailover,
                    ErrorDetails = errorDetails,
                    ResponseBody = responseBody,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens
                });
            }

            _isDirty = true;
        }
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
            if (entry.StatusCode >= 200 && entry.StatusCode < 400 && !string.Equals(entry.Status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                entry.Status = "SUCCESS";
                _successfulRequests++;
            }
            else
            {
                entry.Status = "FAILED";
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

    public PagedResult<ProxyLogEntry> GetPagedLogs(int page = 1, int pageSize = 50, string? filter = null, string? keyword = null)
    {
        lock (_lock)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 500) pageSize = 500;

            var query = _logs.AsEnumerable();

            // 1. 状态过滤 (all / pending / success / error / failover)
            if (!string.IsNullOrWhiteSpace(filter) && !filter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.Equals("pending", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.Status.Equals("PENDING", StringComparison.OrdinalIgnoreCase));
                }
                else if (filter.Equals("failover", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.IsFailover);
                }
                else if (filter.Equals("error", StringComparison.OrdinalIgnoreCase) || filter.Equals("failed", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.Status.Equals("FAILED", StringComparison.OrdinalIgnoreCase) || x.StatusCode >= 400);
                }
                else if (filter.Equals("success", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) || 
                                            (x.StatusCode >= 200 && x.StatusCode < 400 && !x.Status.Equals("FAILED", StringComparison.OrdinalIgnoreCase) && !x.Status.Equals("PENDING", StringComparison.OrdinalIgnoreCase)));
                }
            }

            // 2. 关键字搜索 (路径 / 模型 / 最终渠道 / 尝试渠道 / 状态码 / 错误信息 / 请求载荷 / 响应摘要)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(x => 
                    (x.RequestPath != null && x.RequestPath.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Model != null && x.Model.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (x.FinalChannel != null && x.FinalChannel.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (x.ErrorDetails != null && x.ErrorDetails.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (x.RequestBody != null && x.RequestBody.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (x.ResponseBody != null && x.ResponseBody.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (x.TriedChannels != null && x.TriedChannels.Any(tc => tc.Contains(kw, StringComparison.OrdinalIgnoreCase))) ||
                    x.StatusCode.ToString().Contains(kw) ||
                    x.Status.Contains(kw, StringComparison.OrdinalIgnoreCase)
                );
            }

            var totalCount = query.Count();
            var items = query
                .OrderByDescending(x => x.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<ProxyLogEntry>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            if (_isDirty)
            {
                SaveLogsUnderLock();
            }
        }
    }

    public void Dispose()
    {
        Flush();
        _flushTimer.Dispose();
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
            var pending = _logs.Count(x => x.Status == "PENDING");
            return new DashboardSummary
            {
                TotalChannels = totalChannels,
                ActiveChannels = activeChannels,
                TotalRequests = _totalRequests,
                TotalFailovers = _totalFailovers,
                SuccessfulRequests = _successfulRequests,
                FailedRequests = _failedRequests,
                PendingRequests = pending,
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
            var loadedLogs = new List<ProxyLogEntry>();

            // 1. 读取主日志文件
            if (File.Exists(_logsPath))
            {
                var json = File.ReadAllText(_logsPath);
                var items = JsonSerializer.Deserialize<List<ProxyLogEntry>>(json);
                if (items != null)
                {
                    loadedLogs.AddRange(items);
                }
            }

            // 2. 检查并合并历史分散在 bin/Debug 等输出目录的孤立日志
            var candidatePaths = new List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "request_logs.json"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "data", "request_logs.json")),
                Path.Combine(Directory.GetCurrentDirectory(), "data", "request_logs.json")
            };

            var mergedCount = 0;
            var seenIds = new HashSet<string>(loadedLogs.Select(l => l.Id));

            foreach (var candidate in candidatePaths.Distinct())
            {
                if (File.Exists(candidate) && !string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(_logsPath), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var json = File.ReadAllText(candidate);
                        var extraItems = JsonSerializer.Deserialize<List<ProxyLogEntry>>(json);
                        if (extraItems != null)
                        {
                            foreach (var item in extraItems)
                            {
                                if (seenIds.Add(item.Id))
                                {
                                    loadedLogs.Add(item);
                                    mergedCount++;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略单个文件解析异常
                    }
                }
            }

            lock (_lock)
            {
                _logs.Clear();
                foreach (var item in loadedLogs)
                {
                    // 历史数据平滑迁移：若无明确状态或默认 PENDING 但已有状态码，则自愈补齐为实际完成状态
                    if (string.IsNullOrEmpty(item.Status) || item.Status == "PENDING")
                    {
                        if (item.StatusCode > 0)
                        {
                            item.Status = (item.StatusCode >= 200 && item.StatusCode < 400) ? "SUCCESS" : "FAILED";
                        }
                    }
                    _logs.Add(item);
                }

                _logs.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                _totalRequests = _logs.Count;
                _totalFailovers = _logs.Count(x => x.IsFailover);
                _successfulRequests = _logs.Count(x => x.StatusCode >= 200 && x.StatusCode < 400);
                _failedRequests = _logs.Count(x => x.StatusCode < 200 || x.StatusCode >= 400);

                if (mergedCount > 0)
                {
                    _logger.LogInformation("🎉 历史请求日志自愈完成：已成功合并并恢复 {Count} 条历史日志！", mergedCount);
                    SaveLogsUnderLock();
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
