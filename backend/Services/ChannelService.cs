using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 渠道持久化与状态管理实现类
/// </summary>
public class ChannelService : IChannelService
{
    private readonly string _storagePath;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChannelService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<Channel> _cachedChannels = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ChannelService(
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        ILogger<ChannelService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        
        // NOTE: 数据文件保存在应用数据目录下，保证独立可移植
        var dataDir = Path.Combine(env.ContentRootPath, "data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
        _storagePath = Path.Combine(dataDir, "channels.json");

        LoadInitialData();
    }

    private void LoadInitialData()
    {
        var candidatePaths = new List<string>
        {
            _storagePath,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "channels.json"),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "data", "channels.json")),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "channels.json")
        };

        foreach (var path in candidatePaths.Distinct())
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);
                    var channels = JsonSerializer.Deserialize<List<Channel>>(json, JsonOptions);
                    if (channels != null && channels.Count > 0)
                    {
                        _cachedChannels = channels;
                        _logger.LogInformation("已成功从本地配置文件 [{Path}] 加载 {Count} 个模型渠道", path, _cachedChannels.Count);
                        
                        // 若不是主存储路径，自动保存至主存储路径完成自愈
                        if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(_storagePath), StringComparison.OrdinalIgnoreCase))
                        {
                            SaveToFileInternal();
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "读取本地渠道数据 [{Path}] 失败", path);
                }
            }
        }

        // NOTE: 初始化默认渠道模板，供用户开箱即用填入自身 Key
        _cachedChannels = new List<Channel>
        {
            new()
            {
                Name = "主力中转渠道 (示例)",
                BaseUrl = "https://api.relay-example.com/v1",
                ApiKey = "sk-relay-sample-key",
                Priority = 1,
                IsEnabled = true
            },
            new()
            {
                Name = "备用中转渠道 (示例)",
                BaseUrl = "https://api.backup-relay.com/v1",
                ApiKey = "sk-backup-sample-key",
                Priority = 2,
                IsEnabled = true
            },
            new()
            {
                Name = "官方 Anthropic 兜底 (示例)",
                BaseUrl = "https://api.anthropic.com/v1",
                ApiKey = "sk-ant-sample-key",
                Priority = 3,
                IsEnabled = false
            }
        };

        SaveToFileInternal();
    }

    private void SaveToFileInternal()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cachedChannels, JsonOptions);
            File.WriteAllText(_storagePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "持久化渠道数据至本地文件失败");
        }
    }

    public async Task<List<Channel>> GetAllAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return _cachedChannels
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.CreatedAt)
                .ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Channel?> GetByIdAsync(string id)
    {
        await _semaphore.WaitAsync();
        try
        {
            return _cachedChannels.FirstOrDefault(c => c.Id == id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Channel> CreateAsync(Channel channel)
    {
        await _semaphore.WaitAsync();
        try
        {
            channel.Id = Guid.NewGuid().ToString("N");
            channel.CreatedAt = DateTime.UtcNow;
            channel.UpdatedAt = DateTime.UtcNow;
            
            // NOTE: 自动将新渠道优先级排在末尾
            if (channel.Priority <= 0 || _cachedChannels.Any(c => c.Priority == channel.Priority))
            {
                channel.Priority = (_cachedChannels.MaxBy(c => c.Priority)?.Priority ?? 0) + 1;
            }

            _cachedChannels.Add(channel);
            SaveToFileInternal();
            return channel;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Channel?> UpdateAsync(Channel channel)
    {
        await _semaphore.WaitAsync();
        try
        {
            var existing = _cachedChannels.FirstOrDefault(c => c.Id == channel.Id);
            if (existing == null) return null;

            existing.Name = channel.Name;
            existing.BaseUrl = channel.BaseUrl.TrimEnd('/');
            existing.ApiKey = channel.ApiKey.Trim();
            existing.Models = string.IsNullOrWhiteSpace(channel.Models) ? "*" : channel.Models;
            existing.Group = string.IsNullOrWhiteSpace(channel.Group) ? "all" : channel.Group.Trim();
            existing.ModelMapping = channel.ModelMapping?.Trim() ?? string.Empty;
            existing.CustomHeaders = channel.CustomHeaders?.Trim() ?? string.Empty;
            existing.Priority = channel.Priority;
            existing.IsEnabled = channel.IsEnabled;
            existing.UpdatedAt = DateTime.UtcNow;

            SaveToFileInternal();
            return existing;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await _semaphore.WaitAsync();
        try
        {
            var removed = _cachedChannels.RemoveAll(c => c.Id == id) > 0;
            if (removed)
            {
                SaveToFileInternal();
            }
            return removed;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> ToggleAsync(string id, bool isEnabled)
    {
        await _semaphore.WaitAsync();
        try
        {
            var existing = _cachedChannels.FirstOrDefault(c => c.Id == id);
            if (existing == null) return false;

            existing.IsEnabled = isEnabled;
            if (isEnabled)
            {
                // NOTE: 重新启用时重置失败计数
                existing.FailCount = 0;
                existing.LastFailureReason = null;
            }
            existing.UpdatedAt = DateTime.UtcNow;

            SaveToFileInternal();
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> ReorderAsync(List<string> orderedIds)
    {
        await _semaphore.WaitAsync();
        try
        {
            for (var i = 0; i < orderedIds.Count; i++)
            {
                var channel = _cachedChannels.FirstOrDefault(c => c.Id == orderedIds[i]);
                if (channel != null)
                {
                    channel.Priority = i + 1;
                    channel.UpdatedAt = DateTime.UtcNow;
                }
            }
            SaveToFileInternal();
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<ChannelTestResult> TestChannelAsync(Channel channel)
    {
        var client = _httpClientFactory.CreateClient("ModelTestClient");
        var stopwatch = Stopwatch.StartNew();
        var result = new ChannelTestResult();

        try
        {
            var baseUrl = channel.BaseUrl.TrimEnd('/');
            var keys = channel.GetApiKeys();
            var primaryKey = keys.FirstOrDefault() ?? "";

            // NOTE: 优先测试 /models 列表或轻量级探测，若不支持则尝试探测 messages
            var targetUri = $"{baseUrl}/models";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            
            // 注入自定义请求头模板（包含 Codex 客户端模拟头等）
            var customTemplates = channel.GetCustomHeaderTemplates();
            var hasCustomAuth = false;

            foreach (var (tplKey, tplVal) in customTemplates)
            {
                var resolvedVal = HeaderTemplateResolver.Resolve(
                    tplVal, 
                    null, 
                    primaryKey, 
                    "gpt-5.6-sol", 
                    channel.Group ?? "all");

                if (string.IsNullOrWhiteSpace(resolvedVal)) continue;

                if (tplKey.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                    tplKey.Equals("x-api-key", StringComparison.OrdinalIgnoreCase))
                {
                    hasCustomAuth = true;
                }

                request.Headers.Remove(tplKey);
                request.Headers.TryAddWithoutValidation(tplKey, resolvedVal);
            }

            if (!hasCustomAuth && !string.IsNullOrWhiteSpace(primaryKey))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {primaryKey}");
                request.Headers.TryAddWithoutValidation("x-api-key", primaryKey);
            }
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var response = await client.SendAsync(request, cts.Token);
            stopwatch.Stop();

            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            result.StatusCode = (int)response.StatusCode;

            var body = await response.Content.ReadAsStringAsync();
            result.ResponseSnippet = body.Length > 200 ? body[..200] + "..." : body;

            if (response.IsSuccessStatusCode)
            {
                result.Success = true;
                result.Message = keys.Count > 1 
                    ? $"连接成功 (已配 {keys.Count} 个Key，首Key延迟 {result.LatencyMs}ms)" 
                    : $"连接成功，延迟 {result.LatencyMs}ms";
            }
            else
            {
                result.Success = false;
                result.Message = $"响应 HTTP {result.StatusCode}: {result.ResponseSnippet}";
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.StatusCode = 0;
            result.Message = $"连接失败: {ex.Message}";
        }

        return result;
    }

    public async Task MarkFailureAsync(string channelId, string reason)
    {
        await _semaphore.WaitAsync();
        try
        {
            var channel = _cachedChannels.FirstOrDefault(c => c.Id == channelId);
            if (channel != null)
            {
                channel.FailCount++;
                channel.LastFailureReason = reason;
                channel.UpdatedAt = DateTime.UtcNow;

                // NOTE: 如果连续失败超过阈值或明确欠费，临时熔断禁用
                if (channel.FailCount >= 3 || reason.Contains("欠费") || reason.Contains("insufficient"))
                {
                    channel.IsEnabled = false;
                    _logger.LogWarning("渠道 [{Name}] 触发自动熔断保护并已临时禁用", channel.Name);
                }

                SaveToFileInternal();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task MarkSuccessAsync(string channelId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var channel = _cachedChannels.FirstOrDefault(c => c.Id == channelId);
            if (channel != null)
            {
                channel.FailCount = 0;
                channel.LastFailureReason = null;
                channel.LastSuccessAt = DateTime.UtcNow;
                channel.UpdatedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
