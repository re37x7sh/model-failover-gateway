using System.Collections.Concurrent;
using System.Text.Json;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

public interface ITokenStatsService
{
    void RecordUsage(string channelId, string channelName, string group, string rawApiKey, string? model, long promptTokens, long completionTokens, bool isStream);
    TokenStatsSummaryDto GetSummary();
    List<ChannelTokenStatsDto> GetChannelStats();
    List<KeyTokenStatsDto> GetKeyStats(string? channelId = null);
    void ClearStats();
}

public class TokenStatsService : ITokenStatsService
{
    private readonly ILogger<TokenStatsService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly string _storagePath;
    private readonly List<TokenUsageRecord> _records = new();
    private readonly object _lock = new();
    private bool _isDirty = false;
    private readonly System.Threading.Timer _saveTimer;

    public TokenStatsService(ILogger<TokenStatsService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;

        var dataDir = Path.Combine(_env.ContentRootPath, "data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
        _storagePath = Path.Combine(dataDir, "token_usage.json");

        LoadRecords();

        // 定期检查并将脏数据批量落盘（每 3 秒检查一次）
        _saveTimer = new System.Threading.Timer(_ =>
        {
            if (_isDirty)
            {
                SaveRecords();
            }
        }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    public void RecordUsage(
        string channelId,
        string channelName,
        string group,
        string rawApiKey,
        string? model,
        long promptTokens,
        long completionTokens,
        bool isStream)
    {
        if (promptTokens <= 0 && completionTokens <= 0)
        {
            // 如果未能从响应中解析出有效 token 数，估算保底值或忽略
            promptTokens = 1;
            completionTokens = 1;
        }

        var maskedKey = MaskApiKey(rawApiKey);
        var record = new TokenUsageRecord
        {
            ChannelId = channelId,
            ChannelName = channelName,
            Group = string.IsNullOrWhiteSpace(group) ? "all" : group,
            MaskedKey = maskedKey,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            IsStream = isStream,
            Timestamp = DateTime.UtcNow
        };

        lock (_lock)
        {
            _records.Add(record);
            // 保持最近 20000 条记录以防内存溢出
            if (_records.Count > 20000)
            {
                _records.RemoveRange(0, _records.Count - 20000);
            }
            _isDirty = true;
        }

        _logger.LogInformation("已记录 Token 消耗: 渠道 [{Channel}] Key [{Key}] 模型 [{Model}] -> Prompt: {Prompt}, Completion: {Completion}, Total: {Total}",
            channelName, maskedKey, model ?? "*", promptTokens, completionTokens, promptTokens + completionTokens);
    }

    public TokenStatsSummaryDto GetSummary()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var todayUtc = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            var totalPrompt = _records.Sum(r => r.PromptTokens);
            var totalCompletion = _records.Sum(r => r.CompletionTokens);
            var todayTotal = _records
                .Where(r => r.Timestamp >= todayUtc)
                .Sum(r => r.TotalTokens);

            return new TokenStatsSummaryDto
            {
                TotalTokens = totalPrompt + totalCompletion,
                PromptTokens = totalPrompt,
                CompletionTokens = totalCompletion,
                TodayTokens = todayTotal,
                TotalRequests = _records.Count
            };
        }
    }

    public List<ChannelTokenStatsDto> GetChannelStats()
    {
        lock (_lock)
        {
            var grouped = _records
                .GroupBy(r => r.ChannelId)
                .Select(g =>
                {
                    var first = g.First();
                    return new ChannelTokenStatsDto
                    {
                        ChannelId = g.Key,
                        ChannelName = first.ChannelName,
                        Group = first.Group,
                        PromptTokens = g.Sum(x => x.PromptTokens),
                        CompletionTokens = g.Sum(x => x.CompletionTokens),
                        RequestCount = g.Count(),
                        KeyCount = g.Select(x => x.MaskedKey).Distinct().Count(),
                        LastUsed = g.Max(x => x.Timestamp)
                    };
                })
                .OrderByDescending(x => x.TotalTokens)
                .ToList();

            return grouped;
        }
    }

    public List<KeyTokenStatsDto> GetKeyStats(string? channelId = null)
    {
        lock (_lock)
        {
            var query = _records.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                query = query.Where(r => r.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase));
            }

            var grouped = query
                .GroupBy(r => new { r.ChannelId, r.MaskedKey })
                .Select(g =>
                {
                    var first = g.First();
                    return new KeyTokenStatsDto
                    {
                        ChannelId = g.Key.ChannelId,
                        ChannelName = first.ChannelName,
                        Group = first.Group,
                        MaskedKey = g.Key.MaskedKey,
                        PromptTokens = g.Sum(x => x.PromptTokens),
                        CompletionTokens = g.Sum(x => x.CompletionTokens),
                        RequestCount = g.Count(),
                        LastUsed = g.Max(x => x.Timestamp)
                    };
                })
                .OrderByDescending(x => x.TotalTokens)
                .ToList();

            return grouped;
        }
    }

    public void ClearStats()
    {
        lock (_lock)
        {
            _records.Clear();
            _isDirty = true;
        }
        SaveRecords();
    }

    private void LoadRecords()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                var json = File.ReadAllText(_storagePath);
                var items = JsonSerializer.Deserialize<List<TokenUsageRecord>>(json);
                if (items != null)
                {
                    lock (_lock)
                    {
                        _records.Clear();
                        _records.AddRange(items);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 token_usage.json 失败");
        }
    }

    private void SaveRecords()
    {
        try
        {
            string json;
            lock (_lock)
            {
                _isDirty = false;
                var options = new JsonSerializerOptions { WriteIndented = false };
                json = JsonSerializer.Serialize(_records, options);
            }
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 token_usage.json 失败");
        }
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "N/A";
        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 8) return "sk-***";
        return $"{trimmed.Substring(0, 5)}...{trimmed.Substring(trimmed.Length - 4)}";
    }
}
