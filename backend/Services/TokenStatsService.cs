using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

public interface ITokenStatsService
{
    void RecordUsage(string channelId, string channelName, string group, string rawApiKey, string? model, long promptTokens, long completionTokens, bool isStream, long cacheReadTokens = 0, long cacheCreationTokens = 0);
    TokenStatsSummaryDto GetSummary();
    List<ChannelTokenStatsDto> GetChannelStats();
    List<KeyTokenStatsDto> GetKeyStats(string? channelId = null);
    List<DailyTokenStatsDto> GetDailyStats(int days = 7);
    List<ModelTokenStatsDto> GetModelDistribution();
    string GenerateCsvExport();
    void ClearStats();
    void Flush();
}

public class TokenStatsService : ITokenStatsService, IDisposable
{
    private readonly ILogger<TokenStatsService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly string _storagePath;
    private readonly List<TokenUsageRecord> _records = new();
    private readonly object _lock = new();
    private bool _isDirty = false;
    private readonly System.Threading.Timer _saveTimer;

    // 美元兑人民币参考汇率
    private const double UsdToCnyRate = 7.25;

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

        // 注册进程退出事件，确保优雅停机时立即落盘
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();

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
        bool isStream,
        long cacheReadTokens = 0,
        long cacheCreationTokens = 0)
    {
        if (promptTokens <= 0 && completionTokens <= 0 && cacheReadTokens <= 0)
        {
            // 如果未能从响应中解析出有效 token 数，估算保底值
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
            CacheReadTokens = cacheReadTokens,
            CacheCreationTokens = cacheCreationTokens,
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

        _logger.LogInformation("已记录 Token 消耗: 渠道 [{Channel}] Key [{Key}] 模型 [{Model}] -> Prompt: {Prompt}, Completion: {Completion}, CacheRead: {CacheRead}, Total: {Total}",
            channelName, maskedKey, model ?? "*", promptTokens, completionTokens, cacheReadTokens, record.TotalTokens);
    }

    public TokenStatsSummaryDto GetSummary()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var todayUtc = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            var totalPrompt = _records.Sum(r => r.PromptTokens);
            var totalCompletion = _records.Sum(r => r.CompletionTokens);
            var totalCacheRead = _records.Sum(r => r.CacheReadTokens);
            var totalCacheCreation = _records.Sum(r => r.CacheCreationTokens);
            
            var todayRecords = _records.Where(r => r.Timestamp >= todayUtc).ToList();
            var todayTotal = todayRecords.Sum(r => r.TotalTokens);

            var (totalUsd, totalCny) = CalculateTotalCost(_records);
            var (todayUsd, todayCny) = CalculateTotalCost(todayRecords);
            var (savedUsd, savedCny) = CalculateSavings(_records);

            double cacheHitRate = 0;
            if (totalPrompt + totalCacheRead > 0)
            {
                cacheHitRate = Math.Round((double)totalCacheRead / (totalPrompt + totalCacheRead) * 100.0, 1);
            }

            return new TokenStatsSummaryDto
            {
                TotalTokens = totalPrompt + totalCompletion + totalCacheRead + totalCacheCreation,
                PromptTokens = totalPrompt,
                CompletionTokens = totalCompletion,
                TotalCacheReadTokens = totalCacheRead,
                TotalCacheCreationTokens = totalCacheCreation,
                TodayTokens = todayTotal,
                TotalRequests = _records.Count,
                TotalCostUsd = totalUsd,
                TotalCostCny = totalCny,
                TodayCostUsd = todayUsd,
                TodayCostCny = todayCny,
                TotalSavedCostUsd = savedUsd,
                TotalSavedCostCny = savedCny,
                CacheHitRate = cacheHitRate
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
                    var (usd, cny) = CalculateTotalCost(g);
                    return new ChannelTokenStatsDto
                    {
                        ChannelId = g.Key,
                        ChannelName = first.ChannelName,
                        Group = first.Group,
                        PromptTokens = g.Sum(x => x.PromptTokens),
                        CompletionTokens = g.Sum(x => x.CompletionTokens),
                        CostUsd = usd,
                        CostCny = cny,
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
                    var (usd, cny) = CalculateTotalCost(g);
                    return new KeyTokenStatsDto
                    {
                        ChannelId = g.Key.ChannelId,
                        ChannelName = first.ChannelName,
                        Group = first.Group,
                        MaskedKey = g.Key.MaskedKey,
                        PromptTokens = g.Sum(x => x.PromptTokens),
                        CompletionTokens = g.Sum(x => x.CompletionTokens),
                        CostUsd = usd,
                        CostCny = cny,
                        RequestCount = g.Count(),
                        LastUsed = g.Max(x => x.Timestamp)
                    };
                })
                .OrderByDescending(x => x.TotalTokens)
                .ToList();

            return grouped;
        }
    }

    public List<DailyTokenStatsDto> GetDailyStats(int days = 7)
    {
        if (days < 1) days = 7;
        if (days > 90) days = 90;

        lock (_lock)
        {
            var today = DateTime.UtcNow.Date;
            var startDate = today.AddDays(-days + 1);

            // 按天分组当前范围内的记录
            var recordsInRange = _records.Where(r => r.Timestamp.Date >= startDate).ToList();
            var grouped = recordsInRange
                .GroupBy(r => r.Timestamp.Date.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<DailyTokenStatsDto>();
            for (var i = 0; i < days; i++)
            {
                var curDate = startDate.AddDays(i);
                var dateKey = curDate.ToString("yyyy-MM-dd");

                if (grouped.TryGetValue(dateKey, out var dailyList))
                {
                    var (usd, cny) = CalculateTotalCost(dailyList);
                    result.Add(new DailyTokenStatsDto
                    {
                        Date = dateKey,
                        PromptTokens = dailyList.Sum(x => x.PromptTokens),
                        CompletionTokens = dailyList.Sum(x => x.CompletionTokens),
                        CostUsd = usd,
                        CostCny = cny,
                        RequestCount = dailyList.Count
                    });
                }
                else
                {
                    result.Add(new DailyTokenStatsDto
                    {
                        Date = dateKey,
                        PromptTokens = 0,
                        CompletionTokens = 0,
                        CostUsd = 0,
                        CostCny = 0,
                        RequestCount = 0
                    });
                }
            }

            return result;
        }
    }

    public List<ModelTokenStatsDto> GetModelDistribution()
    {
        lock (_lock)
        {
            var totalTokens = _records.Sum(r => r.TotalTokens);
            if (totalTokens == 0) return new List<ModelTokenStatsDto>();

            var grouped = _records
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Model) ? "Unknown / Default" : r.Model)
                .Select(g =>
                {
                    var (usd, cny) = CalculateTotalCost(g);
                    var modelTotal = g.Sum(x => x.TotalTokens);
                    return new ModelTokenStatsDto
                    {
                        Model = g.Key,
                        PromptTokens = g.Sum(x => x.PromptTokens),
                        CompletionTokens = g.Sum(x => x.CompletionTokens),
                        CostUsd = usd,
                        CostCny = cny,
                        RequestCount = g.Count(),
                        Percentage = Math.Round((double)modelTotal / totalTokens * 100, 2)
                    };
                })
                .OrderByDescending(x => x.TotalTokens)
                .ToList();

            return grouped;
        }
    }

    public string GenerateCsvExport()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            // UTF-8 BOM
            sb.Append('\uFEFF');
            sb.AppendLine("ID,时间(UTC),渠道名称,分组,脱敏Key,模型,PromptTokens,CompletionTokens,TotalTokens,预估费用(USD),预估费用(CNY),是否流式");

            foreach (var r in _records.OrderByDescending(x => x.Timestamp))
            {
                var (usd, cny) = CalculateCost(r.Model, r.PromptTokens, r.CompletionTokens);
                var isStreamStr = r.IsStream ? "是" : "否";
                sb.AppendLine($"\"{r.Id}\",\"{r.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{EscapeCsv(r.ChannelName)}\",\"{EscapeCsv(r.Group)}\",\"{r.MaskedKey}\",\"{EscapeCsv(r.Model ?? "-")}\",{r.PromptTokens},{r.CompletionTokens},{r.TotalTokens},{usd:F4},{cny:F4},\"{isStreamStr}\"");
            }

            return sb.ToString();
        }
    }

    private static string EscapeCsv(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("\"", "\"\"");
    }

    public static (double usd, double cny) CalculateCost(string? model, long promptTokens, long completionTokens)
    {
        var m = (model ?? "").ToLowerInvariant();
        double promptRate = 1.50;    // USD per 1M tokens
        double compRate = 4.50;      // USD per 1M tokens

        // Claude 3.7 / 3.5 Sonnet 系列
        if (m.Contains("claude-3-7") || m.Contains("claude-3.7") || m.Contains("claude-3-5-sonnet") || m.Contains("claude-3.5-sonnet") || m.Contains("sonnet"))
        {
            promptRate = 3.00;
            compRate = 15.00;
        }
        // Claude 3.5 Haiku 系列
        else if (m.Contains("haiku"))
        {
            promptRate = 0.80;
            compRate = 4.00;
        }
        // Claude Opus 系列
        else if (m.Contains("opus"))
        {
            promptRate = 15.00;
            compRate = 75.00;
        }
        // GPT-4o Mini 系列
        else if (m.Contains("4o-mini") || m.Contains("gpt-4o-mini"))
        {
            promptRate = 0.15;
            compRate = 0.60;
        }
        // GPT-4o 系列
        else if (m.Contains("4o") || m.Contains("gpt-4o"))
        {
            promptRate = 2.50;
            compRate = 10.00;
        }
        // GPT-5 / GPT-5.6 / o1 / o3 系列
        else if (m.Contains("gpt-5") || m.Contains("o1") || m.Contains("o3"))
        {
            promptRate = 5.00;
            compRate = 15.00;
        }
        // DeepSeek-R1 (Reasoner)
        else if (m.Contains("deepseek-r1") || m.Contains("reasoner") || m.Contains("r1"))
        {
            promptRate = 0.55;
            compRate = 2.19;
        }
        // DeepSeek-V3 / DeepSeek-Chat
        else if (m.Contains("deepseek"))
        {
            promptRate = 0.14;
            compRate = 0.28;
        }
        // GPT-4 Turbo
        else if (m.Contains("gpt-4"))
        {
            promptRate = 10.00;
            compRate = 30.00;
        }

        var usd = (promptTokens * promptRate + completionTokens * compRate) / 1_000_000.0;
        var cny = usd * UsdToCnyRate;
        return (Math.Round(usd, 4), Math.Round(cny, 4));
    }

    private static (double usd, double cny) CalculateTotalCost(IEnumerable<TokenUsageRecord> records)
    {
        double totalUsd = 0;
        double totalCny = 0;

        foreach (var r in records)
        {
            var (usd, cny) = CalculateCost(r.Model, r.PromptTokens, r.CompletionTokens);
            totalUsd += usd;
            totalCny += cny;
        }

        return (Math.Round(totalUsd, 4), Math.Round(totalCny, 4));
    }

    private static (double savedUsd, double savedCny) CalculateSavings(IEnumerable<TokenUsageRecord> records)
    {
        double totalSavedUsd = 0;
        foreach (var r in records)
        {
            if (r.CacheReadTokens <= 0) continue;
            var m = (r.Model ?? "").ToLowerInvariant();
            double promptRate = 3.00;
            if (m.Contains("haiku")) promptRate = 0.80;
            else if (m.Contains("opus")) promptRate = 15.00;
            else if (m.Contains("4o-mini")) promptRate = 0.15;
            else if (m.Contains("4o")) promptRate = 2.50;
            else if (m.Contains("gpt-5") || m.Contains("o1") || m.Contains("o3")) promptRate = 5.00;
            else if (m.Contains("deepseek-r1")) promptRate = 0.55;
            else if (m.Contains("deepseek")) promptRate = 0.14;
            else if (m.Contains("gpt-4")) promptRate = 10.00;

            // Prompt Caching 命中读取节省 90% 的输入成本
            var savedUsd = (r.CacheReadTokens * promptRate * 0.90) / 1_000_000.0;
            totalSavedUsd += savedUsd;
        }

        var totalSavedCny = totalSavedUsd * UsdToCnyRate;
        return (Math.Round(totalSavedUsd, 4), Math.Round(totalSavedCny, 4));
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

    public void Flush()
    {
        lock (_lock)
        {
            if (_isDirty)
            {
                SaveRecordsUnderLock();
            }
        }
    }

    public void Dispose()
    {
        Flush();
        _saveTimer.Dispose();
    }

    private void LoadRecords()
    {
        try
        {
            var loadedRecords = new List<TokenUsageRecord>();

            // 1. 读取主存储文件
            if (File.Exists(_storagePath))
            {
                var json = File.ReadAllText(_storagePath);
                var items = JsonSerializer.Deserialize<List<TokenUsageRecord>>(json);
                if (items != null)
                {
                    loadedRecords.AddRange(items);
                }
            }

            // 2. 智能合并潜在分散在 bin/Debug 或其他路径的历史孤立文件（避免因启动目录不同导致数据丢失）
            var candidatePaths = new List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "token_usage.json"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "data", "token_usage.json")),
                Path.Combine(Directory.GetCurrentDirectory(), "data", "token_usage.json")
            };

            var mergedCount = 0;
            var seenKeys = new HashSet<string>(
                loadedRecords.Select(r => $"{r.ChannelId}_{r.MaskedKey}_{r.Timestamp.Ticks}_{r.PromptTokens}_{r.CompletionTokens}")
            );

            foreach (var candidate in candidatePaths.Distinct())
            {
                if (File.Exists(candidate) && !string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(_storagePath), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var json = File.ReadAllText(candidate);
                        var extraItems = JsonSerializer.Deserialize<List<TokenUsageRecord>>(json);
                        if (extraItems != null)
                        {
                            foreach (var item in extraItems)
                            {
                                var key = $"{item.ChannelId}_{item.MaskedKey}_{item.Timestamp.Ticks}_{item.PromptTokens}_{item.CompletionTokens}";
                                if (seenKeys.Add(key))
                                {
                                    loadedRecords.Add(item);
                                    mergedCount++;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略单个文件的读取错误
                    }
                }
            }

            lock (_lock)
            {
                _records.Clear();
                _records.AddRange(loadedRecords.OrderBy(r => r.Timestamp));
                if (_records.Count > 20000)
                {
                    _records.RemoveRange(0, _records.Count - 20000);
                }

                if (mergedCount > 0)
                {
                    _logger.LogInformation("🎉 历史数据自愈完成：已成功从历史孤立目录合并并恢复 {Count} 条 Token 消耗记录！", mergedCount);
                    SaveRecordsUnderLock();
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
        lock (_lock)
        {
            SaveRecordsUnderLock();
        }
    }

    private void SaveRecordsUnderLock()
    {
        try
        {
            _isDirty = false;
            var options = new JsonSerializerOptions { WriteIndented = false };
            var json = JsonSerializer.Serialize(_records, options);
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
