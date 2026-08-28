using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 核心透明代理与故障转移引擎实现
/// </summary>
public class ProxyEngine : IProxyEngine
{
    private readonly IChannelService _channelService;
    private readonly ILogService _logService;
    private readonly ITokenStatsService _tokenStatsService;
    private readonly IAlertService _alertService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProxyEngine> _logger;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, HttpClient> _proxyClients = new();

    // NOTE: 过滤传输层逐跳（Hop-by-hop）Header，避免协议冲突
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "Proxy-Connection", "Proxy-Authenticate", "Proxy-Authorization"
    };

    public ProxyEngine(
        IChannelService channelService,
        ILogService logService,
        ITokenStatsService tokenStatsService,
        IAlertService alertService,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment env,
        ILogger<ProxyEngine> logger)
    {
        _channelService = channelService;
        _logService = logService;
        _tokenStatsService = tokenStatsService;
        _alertService = alertService;
        _httpClientFactory = httpClientFactory;
        _env = env;
        _logger = logger;
    }

    private HttpClient GetProxyClient(string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            return _httpClientFactory.CreateClient("ModelProxyClient");
        }

        return _proxyClients.GetOrAdd(proxyUrl.Trim(), url =>
        {
            var handler = new SocketsHttpHandler
            {
                Proxy = new System.Net.WebProxy(url),
                UseProxy = true,
                ConnectTimeout = TimeSpan.FromSeconds(15)
            };
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        });
    }

    private bool IsGatewayAuthRequired(HttpContext context, out string error)
    {
        error = string.Empty;
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "data");
            var settingsPath = Path.Combine(dataDir, "gateway_settings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<GatewaySettings>(json);
                if (settings != null && settings.RequireAuth && !string.IsNullOrWhiteSpace(settings.AuthToken))
                {
                    // 检查客户端请求头：Authorization: Bearer <Token> 或 x-api-key: <Token>
                    var clientToken = "";
                    if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                    {
                        var str = authHeader.ToString().Trim();
                        if (str.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            clientToken = str["Bearer ".Length..].Trim();
                        }
                        else
                        {
                            clientToken = str;
                        }
                    }
                    else if (context.Request.Headers.TryGetValue("x-api-key", out var keyHeader))
                    {
                        clientToken = keyHeader.ToString().Trim();
                    }

                    if (!string.Equals(clientToken, settings.AuthToken.Trim(), StringComparison.Ordinal))
                    {
                        error = "网关已启用安全访问鉴权，请在客户端配置有效的 Gateway Token (Authorization: Bearer <Token> 或 x-api-key: <Token>)";
                        return true;
                    }
                }
            }
        }
        catch
        {
            // 忽略读取异常
        }

        return false;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _roundRobinIndices = new();

    private List<Channel> ApplyLoadBalancingStrategy(List<Channel> channels, string group)
    {
        if (channels.Count <= 1) return channels;

        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "data");
            var settingsPath = Path.Combine(dataDir, "gateway_settings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<GatewaySettings>(json);
                if (settings != null)
                {
                    var strategy = (settings.LoadBalancingStrategy ?? "priority").ToLowerInvariant();
                    if (strategy == "round_robin")
                    {
                        var idx = _roundRobinIndices.AddOrUpdate(group, 0, (_, v) => (v + 1) % channels.Count);
                        return channels.Skip(idx).Concat(channels.Take(idx)).ToList();
                    }
                    if (strategy == "random")
                    {
                        return channels.OrderBy(_ => Random.Shared.Next()).ToList();
                    }
                }
            }
        }
        catch { }

        return channels; // 默认 priority 模式 (已按 Priority 升序排序)
    }

    public async Task ForwardRequestAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var rawPath = context.Request.Path.Value ?? "/";
        var requestMethod = context.Request.Method;

        // NOTE: 解析请求路径中的分组前缀（如 /claude/v1/*、/codex/v1/* 或根 /claude），并还原标准上游路径
        string requestedGroup = "all";
        string cleanRequestPath = rawPath;

        var segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && segments[1].Equals("v1", StringComparison.OrdinalIgnoreCase))
        {
            requestedGroup = segments[0];
            cleanRequestPath = "/" + string.Join('/', segments.Skip(1));
        }
        else if (segments.Length == 1 && !segments[0].Equals("v1", StringComparison.OrdinalIgnoreCase))
        {
            requestedGroup = segments[0];
            cleanRequestPath = "/";
        }
        else if (context.Request.Headers.TryGetValue("X-Gateway-Group", out var groupHeader))
        {
            requestedGroup = groupHeader.ToString();
        }

        // NOTE: 快速响应客户端探针（如 Claude Code 初始化时发送的 HEAD /claude 或 GET /claude）
        if ((requestMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase) || (requestMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && requestedGroup != "all")) &&
            (cleanRequestPath == "/" || cleanRequestPath == "/v1" || string.IsNullOrEmpty(cleanRequestPath)))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"status\":\"ok\",\"gateway\":\"Model Failover Gateway\",\"group\":\"{requestedGroup}\"}}");
            return;
        }

        // NOTE: 浏览器直接访问根路径或前端路由时，返回 Vue SPA 主页
        if (requestMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && 
            (cleanRequestPath == "/" || string.IsNullOrEmpty(cleanRequestPath)) && 
            requestedGroup == "all")
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            if (!Directory.Exists(wwwroot))
            {
                wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            }
            var indexPath = Path.Combine(wwwroot, "index.html");
            if (File.Exists(indexPath))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers.Expires = "0";
                await context.Response.SendFileAsync(indexPath);
                return;
            }
        }

        // 0. 校验网关安全访问鉴权 (如果开启了 RequireAuth)
        if (IsGatewayAuthRequired(context, out var authError))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"error\": {{\"message\": \"{authError}\"}}}}");

            _logService.AddLog(new ProxyLogEntry
            {
                ClientIp = clientIp,
                RequestMethod = requestMethod,
                RequestPath = rawPath,
                StatusCode = 401,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorDetails = authError
            });
            return;
        }

        // 1. 读取并缓存客户端原始请求体字节（为了在失败重试时可以重复向不同渠道发送）
        context.Request.EnableBuffering();
        using var memoryStream = new MemoryStream();
        await context.Request.Body.CopyToAsync(memoryStream);
        var rawRequestBody = memoryStream.ToArray();

        // 采集客户端安全请求头（脱敏敏感鉴权字段）用于日志排查与渠道嗅探提取
        var clientHeadersForLog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hKey, hVals) in context.Request.Headers)
        {
            if (hKey.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                var authStr = hVals.ToString();
                clientHeadersForLog[hKey] = authStr.Length > 15 ? $"{authStr[..10]}***" : "Bearer ***";
            }
            else if (hKey.Equals("x-api-key", StringComparison.OrdinalIgnoreCase))
            {
                var keyStr = hVals.ToString();
                clientHeadersForLog[hKey] = keyStr.Length > 10 ? $"{keyStr[..6]}***" : "***";
            }
            else
            {
                clientHeadersForLog[hKey] = hVals.ToString();
            }
        }

        // 尝试从请求体中提取 model 字段供日志与匹配参考
        string? requestedModel = null;
        if (rawRequestBody.Length > 0)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(rawRequestBody);
                if (jsonDoc.RootElement.TryGetProperty("model", out var modelProp))
                {
                    requestedModel = modelProp.GetString();
                }
            }
            catch
            {
                // NOTE: 非 JSON 请求体则忽略模型提取
            }
        }

        // 2. 获取当前所有可用的激活渠道，并按请求分组过滤
        var allChannels = await _channelService.GetAllAsync();
        var activeChannels = allChannels
            .Where(c => c.IsEnabled && c.IsGroupMatch(requestedGroup))
            .ToList();

        // 如果用户请求了特定模型，且渠道指定了模型列表，可优先匹配（默认 * 匹配全部）
        if (!string.IsNullOrEmpty(requestedModel))
        {
            var matchedChannels = activeChannels.Where(c => IsModelSupported(c.Models, requestedModel)).ToList();
            if (matchedChannels.Any())
            {
                activeChannels = matchedChannels;
            }
        }

        // 应用全局负载均衡分流策略（轮询 / 随机 / 优先级）
        activeChannels = ApplyLoadBalancingStrategy(activeChannels, requestedGroup);

        if (!activeChannels.Any())
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            var errMsg = requestedGroup.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? "没有可用或启用的模型渠道，请在 Web 面板添加或启用渠道"
                : $"分组 [{requestedGroup}] 下没有可用或启用的模型渠道，请在 Web 面板配置对应分组渠道";
            await context.Response.WriteAsync($"{{\"error\": {{\"message\": \"{errMsg}\"}}}}");

            _logService.AddLog(new ProxyLogEntry
            {
                ClientIp = clientIp,
                RequestMethod = requestMethod,
                RequestPath = rawPath,
                Model = requestedModel,
                StatusCode = 503,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorDetails = errMsg
            });
            return;
        }

        var triedChannels = new List<string>();
        var isFailoverOccurred = false;
        string? lastErrorDetails = null;

        // 驱动桌面宠物与状态监控进入思考状态
        _alertService.NotifyTaskStart(requestedModel ?? "AI Model", activeChannels.First().Name);

        // 3. 按照优先级顺序依次尝试渠道
        for (var i = 0; i < activeChannels.Count; i++)
        {
            var channel = activeChannels[i];

            // 智能熔断检查：若渠道正处于熔断冷却期，直接秒级跳过，避免客户端等待超时
            if (channel.IsCircuitBroken)
            {
                var remainingSec = channel.CircuitBreakerRemainingSeconds;
                _logger.LogWarning("渠道 [{Name}] 处于智能熔断冷却中 (剩余 {Sec}s)，已自动快速跳过", channel.Name, remainingSec);
                triedChannels.Add($"{channel.Name} (⚡熔断中，剩余{remainingSec}s)");
                isFailoverOccurred = true;
                continue;
            }

            var client = GetProxyClient(channel.ProxyUrl);
            var keys = channel.GetApiKeys();
            if (keys.Count == 0)
            {
                keys = new List<string> { "" };
            }

            // NOTE: 处理模型别名映射（如：将客户端的 claude-3-7-sonnet 重写为 gpt-5.6-sol）
            var effectiveModel = requestedModel != null ? channel.GetMappedModel(requestedModel) : requestedModel;
            var effectiveRequestBody = rawRequestBody;
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(effectiveModel) && requestedModel != effectiveModel && rawRequestBody.Length > 0)
            {
                effectiveRequestBody = RewriteModelInRequestBody(rawRequestBody, effectiveModel);
            }

            var channelSuccess = false;

            // NOTE: 支持单渠道配置多个 API Key 时的键级容灾与轮换
            for (var k = 0; k < keys.Count; k++)
            {
                var apiKey = keys[k];
                var channelLabel = keys.Count > 1 ? $"{channel.Name} (Key #{k + 1})" : channel.Name;
                if (!string.IsNullOrEmpty(effectiveModel) && effectiveModel != requestedModel)
                {
                    channelLabel += $" -> {effectiveModel}";
                }
                triedChannels.Add(channelLabel);

                try
                {
                    var targetUrl = BuildTargetUrl(channel.BaseUrl, cleanRequestPath, context.Request.QueryString.Value);
                    using var upstreamRequest = new HttpRequestMessage(new HttpMethod(requestMethod), targetUrl);

                    // 4. 100% 完整镜像透传 Header（保留 Claude Prompt Caching 的 anthropic-beta 等）
                    foreach (var (headerKey, headerValues) in context.Request.Headers)
                    {
                        if (HopByHopHeaders.Contains(headerKey) ||
                            headerKey.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                            headerKey.Equals("x-api-key", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        upstreamRequest.Headers.TryAddWithoutValidation(headerKey, headerValues.ToArray());
                    }

                    // 4.1 动态解析并注入渠道自定义请求头（支持 Codex 伪装预设与 {header:X-Name} 动态提取）
                    var customTemplates = channel.GetCustomHeaderTemplates();
                    var hasCustomAuth = false;

                    foreach (var (tplKey, tplVal) in customTemplates)
                    {
                        var resolvedVal = HeaderTemplateResolver.Resolve(
                            tplVal,
                            context,
                            apiKey,
                            effectiveModel ?? requestedModel ?? string.Empty,
                            requestedGroup);

                        if (string.IsNullOrWhiteSpace(resolvedVal)) continue;

                        if (tplKey.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                            tplKey.Equals("x-api-key", StringComparison.OrdinalIgnoreCase))
                        {
                            hasCustomAuth = true;
                        }

                        // 覆盖可能已从客户端透传的旧 Header（如 User-Agent、Editor-Version 等）
                        upstreamRequest.Headers.Remove(tplKey);
                        upstreamRequest.Headers.TryAddWithoutValidation(tplKey, resolvedVal);
                    }

                    // 4.2 若自定义请求头中未显式指定鉴权，则注入当前渠道的 API Key
                    if (!hasCustomAuth && !string.IsNullOrWhiteSpace(apiKey))
                    {
                        upstreamRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
                        upstreamRequest.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                    }

                    // 5. 填入（可能经过模型别名重写的）请求体
                    if (effectiveRequestBody.Length > 0)
                    {
                        var content = new ByteArrayContent(effectiveRequestBody);
                        if (context.Request.ContentType != null)
                        {
                            content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
                        }
                        upstreamRequest.Content = content;
                    }

                    // 6. 发送请求给上游渠道
                    var upstreamResponse = await client.SendAsync(
                        upstreamRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        context.RequestAborted);

                    // 7. 检查响应：若发生欠费/限流等故障，则触发 Failover
                    if (!upstreamResponse.IsSuccessStatusCode)
                    {
                        var errorBytes = await upstreamResponse.Content.ReadAsByteArrayAsync(context.RequestAborted);
                        var errorText = Encoding.UTF8.GetString(errorBytes);

                        if (ShouldTriggerFailover((int)upstreamResponse.StatusCode, errorText))
                        {
                            _logger.LogWarning("渠道 [{Label}] 触发限流或欠费报警 (HTTP {Code})，正在尝试下一个 Key 或渠道... 详情: {Error}",
                                channelLabel, (int)upstreamResponse.StatusCode, errorText);

                            lastErrorDetails = $"[{channelLabel}] HTTP {(int)upstreamResponse.StatusCode}: {errorText}";
                            _alertService.AddAlert(channel.Id, channelLabel, channel.Group ?? "all", $"HTTP {(int)upstreamResponse.StatusCode} 欠费/限流，已自动切换备用");
                            isFailoverOccurred = true;
                            continue; // 尝试当前渠道的下一个 Key 或下一个渠道
                        }

                        // 如果不是余额/限流问题（例如客户端 prompt 400 格式错误），正常透传原样错误
                        stopwatch.Stop();
                        await WriteFullResponseAsync(context, upstreamResponse, errorBytes);
                        
                        _logService.AddLog(new ProxyLogEntry
                        {
                            ClientIp = clientIp,
                            RequestMethod = requestMethod,
                            RequestPath = rawPath,
                            Model = requestedModel,
                            TriedChannels = triedChannels,
                            FinalChannel = channelLabel,
                            StatusCode = (int)upstreamResponse.StatusCode,
                            DurationMs = stopwatch.ElapsedMilliseconds,
                            IsFailover = isFailoverOccurred,
                            ErrorDetails = errorText,
                            RequestHeaders = clientHeadersForLog
                        });
                        return;
                    }

                    // 8. 成功响应：100% 字节管道直通（保持 SSE 打字机流式输出）
                    await _channelService.MarkSuccessAsync(channel.Id);
                    
                    await StreamPipeResponseWithTokenTrackingAsync(
                        context, 
                        upstreamResponse, 
                        channel.Id, 
                        channel.Name, 
                        channel.Group ?? "all", 
                        apiKey, 
                        effectiveModel ?? requestedModel, 
                        effectiveRequestBody,
                        stopwatch);

                    stopwatch.Stop();

                    _logService.AddLog(new ProxyLogEntry
                    {
                        ClientIp = clientIp,
                        RequestMethod = requestMethod,
                        RequestPath = rawPath,
                        Model = requestedModel,
                        TriedChannels = triedChannels,
                        FinalChannel = channelLabel,
                        StatusCode = (int)upstreamResponse.StatusCode,
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        IsFailover = isFailoverOccurred,
                        RequestHeaders = clientHeadersForLog
                    });

                    channelSuccess = true;
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "请求渠道 [{Label}] 发生网络异常: {Message}", channelLabel, ex.Message);
                    lastErrorDetails = $"[{channelLabel}] 网络异常: {ex.Message}";
                    _alertService.AddAlert(channel.Id, channelLabel, channel.Group ?? "all", $"网络异常: {ex.Message}");
                    _alertService.NotifyTaskFailover(effectiveModel ?? requestedModel ?? "AI Model", channelLabel, $"网络异常: {ex.Message}");

                    // 如果已经开始向客户端流式输出响应体，无法在中断后再切换渠道或重设状态码
                    if (context.Response.HasStarted)
                    {
                        _logger.LogWarning("流式响应在传输中途中断，因已向客户端发送数据，无法切换备用渠道");
                        return;
                    }

                    isFailoverOccurred = true;
                }
            }

            if (!channelSuccess)
            {
                await _channelService.MarkFailureAsync(channel.Id, lastErrorDetails ?? "所有 Key 尝试均失败");
            }
        }

        // 9. 所有渠道均尝试失败
        stopwatch.Stop();
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            context.Response.ContentType = "application/json";
            var failureResponse = JsonSerializer.Serialize(new
            {
                error = new
                {
                    message = "所有配置的可用上游渠道均尝试失败",
                    tried_channels = triedChannels,
                    last_error = lastErrorDetails
                }
            });
            await context.Response.WriteAsync(failureResponse);
        }

        _logService.AddLog(new ProxyLogEntry
        {
            ClientIp = clientIp,
            RequestMethod = requestMethod,
            RequestPath = rawPath,
            Model = requestedModel,
            TriedChannels = triedChannels,
            FinalChannel = null,
            StatusCode = 502,
            DurationMs = stopwatch.ElapsedMilliseconds,
            IsFailover = true,
            ErrorDetails = lastErrorDetails ?? "所有上游渠道均不可用",
            RequestHeaders = clientHeadersForLog
        });
    }

    /// <summary>
    /// 组合目标请求 URL，自动处理 /v1 路径拼接
    /// </summary>
    private static string BuildTargetUrl(string baseUrl, string requestPath, string? queryString)
    {
        var cleanBase = baseUrl.TrimEnd('/');
        var cleanPath = requestPath.TrimStart('/');

        if (cleanBase.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) && cleanPath.StartsWith("v1/", StringComparison.OrdinalIgnoreCase))
        {
            cleanPath = cleanPath.Substring(3);
        }

        var fullUrl = $"{cleanBase}/{cleanPath}";
        if (!string.IsNullOrEmpty(queryString))
        {
            fullUrl += queryString;
        }
        return fullUrl;
    }

    /// <summary>
    /// 判断是否命中自动切换规则（余额不足、欠费、429、502、503、Overloaded 等）
    /// </summary>
    private static bool ShouldTriggerFailover(int statusCode, string responseBody)
    {
        if (statusCode == StatusCodes.Status429TooManyRequests ||
            statusCode == StatusCodes.Status503ServiceUnavailable ||
            statusCode == StatusCodes.Status504GatewayTimeout)
        {
            return true;
        }

        if (statusCode == StatusCodes.Status401Unauthorized ||
            statusCode == StatusCodes.Status402PaymentRequired ||
            statusCode == StatusCodes.Status403Forbidden ||
            statusCode == StatusCodes.Status400BadRequest)
        {
            var lower = responseBody.ToLowerInvariant();
            if (lower.Contains("quota") ||
                lower.Contains("balance") ||
                lower.Contains("insufficient") ||
                lower.Contains("credit") ||
                lower.Contains("rate limit") ||
                lower.Contains("rate_limit") ||
                lower.Contains("overloaded") ||
                lower.Contains("exceeded") ||
                lower.Contains("欠费") ||
                lower.Contains("额度不足") ||
                lower.Contains("余额不足") ||
                lower.Contains("超额"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsModelSupported(string supportedModels, string targetModel)
    {
        if (string.IsNullOrWhiteSpace(supportedModels) || supportedModels.Trim() == "*") return true;
        var list = supportedModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return list.Any(m => m.Equals(targetModel, StringComparison.OrdinalIgnoreCase) || m == "*");
    }

    private async Task StreamPipeResponseWithTokenTrackingAsync(
        HttpContext context,
        HttpResponseMessage upstreamResponse,
        string channelId,
        string channelName,
        string group,
        string rawApiKey,
        string? model,
        byte[] requestBodyBytes,
        Stopwatch stopwatch)
    {
        context.Response.StatusCode = (int)upstreamResponse.StatusCode;

        foreach (var header in upstreamResponse.Headers)
        {
            if (!HopByHopHeaders.Contains(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }
        foreach (var header in upstreamResponse.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        await using var responseStream = await upstreamResponse.Content.ReadAsStreamAsync(context.RequestAborted);
        var buffer = new byte[8192];
        var responseSampleBuilder = new StringBuilder();
        var totalBytesTransferred = 0L;
        int bytesRead;

        try
        {
            while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, context.RequestAborted)) > 0)
            {
                await context.Response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), context.RequestAborted);
                totalBytesTransferred += bytesRead;

                if (responseSampleBuilder.Length < 65536)
                {
                    var chunkText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    responseSampleBuilder.Append(chunkText);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "渠道 [{Channel}] 流式响应在传输中途中断 (上游连接意外关闭/EOF): {Message}", channelName, ex.Message);
        }

        // 提取并在后台安全记录 Token 与触发完成事件
        try
        {
            var sampleText = responseSampleBuilder.ToString();
            var (promptTokens, completionTokens, cacheReadTokens, cacheCreationTokens) = ExtractTokens(sampleText, requestBodyBytes.Length, totalBytesTransferred);

            _tokenStatsService.RecordUsage(
                channelId,
                channelName,
                group,
                rawApiKey,
                model,
                promptTokens,
                completionTokens,
                isStream: true,
                cacheReadTokens: cacheReadTokens,
                cacheCreationTokens: cacheCreationTokens);

            var (isToolCall, stopReason) = ExtractStopReason(sampleText);

            // 触发长任务完成主动通知与桌面宠物联动 (精准区分工具调用与最终回答)
            _alertService.NotifyTaskComplete(
                model ?? "AI Model", 
                channelName, 
                stopwatch.ElapsedMilliseconds, 
                promptTokens + completionTokens + cacheReadTokens,
                isToolCall,
                stopReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token 统计提取与任务完成通知异常");
        }
    }

    /// <summary>
    /// 识别流式响应末尾的 stop_reason / finish_reason，判断是否为工具调用中间环节
    /// </summary>
    private static (bool isToolCall, string stopReason) ExtractStopReason(string sampleText)
    {
        if (string.IsNullOrEmpty(sampleText)) return (false, "");

        // 1. Claude 格式: "stop_reason": "tool_use" / "end_turn"
        var stopReasonMatch = System.Text.RegularExpressions.Regex.Match(sampleText, @"""stop_reason""\s*:\s*""([^""]+)""");
        if (stopReasonMatch.Success)
        {
            var reason = stopReasonMatch.Groups[1].Value.Trim().ToLowerInvariant();
            if (reason == "tool_use")
            {
                return (true, "tool_use");
            }
            if (reason is "end_turn" or "stop_sequence")
            {
                return (false, reason);
            }
        }

        // 2. OpenAI / Codex 格式: "finish_reason": "tool_calls" / "stop"
        var finishReasonMatch = System.Text.RegularExpressions.Regex.Match(sampleText, @"""finish_reason""\s*:\s*""([^""]+)""");
        if (finishReasonMatch.Success)
        {
            var reason = finishReasonMatch.Groups[1].Value.Trim().ToLowerInvariant();
            if (reason is "tool_calls" or "function_call")
            {
                return (true, reason);
            }
            if (reason is "stop")
            {
                return (false, "stop");
            }
        }

        // 3. 增强保底判定: 如果响应体中包含明确的 tool_use 块
        if (sampleText.Contains("\"type\":\"tool_use\"") || 
            sampleText.Contains("\"type\": \"tool_use\"") || 
            sampleText.Contains("\"tool_calls\"") || 
            sampleText.Contains("\"function_call\""))
        {
            return (true, "tool_use");
        }

        return (false, "");
    }

    private static (long promptTokens, long completionTokens, long cacheReadTokens, long cacheCreationTokens) ExtractTokens(string responseText, int requestBodyLength, long responseLength)
    {
        long prompt = 0;
        long completion = 0;
        long cacheRead = 0;
        long cacheCreation = 0;

        try
        {
            // 1. 匹配 Claude 格式 (取最大值以获取最终 delta usage)
            var inputMatches = System.Text.RegularExpressions.Regex.Matches(responseText, @"""input_tokens""\s*:\s*(\d+)");
            foreach (System.Text.RegularExpressions.Match m in inputMatches)
            {
                if (long.TryParse(m.Groups[1].Value, out var val) && val > prompt)
                {
                    prompt = val;
                }
            }

            var outputMatches = System.Text.RegularExpressions.Regex.Matches(responseText, @"""output_tokens""\s*:\s*(\d+)");
            foreach (System.Text.RegularExpressions.Match m in outputMatches)
            {
                if (long.TryParse(m.Groups[1].Value, out var val) && val > completion)
                {
                    completion = val;
                }
            }

            var cacheReadMatches = System.Text.RegularExpressions.Regex.Matches(responseText, @"""cache_read_input_tokens""\s*:\s*(\d+)");
            foreach (System.Text.RegularExpressions.Match m in cacheReadMatches)
            {
                if (long.TryParse(m.Groups[1].Value, out var val) && val > cacheRead)
                {
                    cacheRead = val;
                }
            }

            var cacheCreateMatches = System.Text.RegularExpressions.Regex.Matches(responseText, @"""cache_creation_input_tokens""\s*:\s*(\d+)");
            foreach (System.Text.RegularExpressions.Match m in cacheCreateMatches)
            {
                if (long.TryParse(m.Groups[1].Value, out var val) && val > cacheCreation)
                {
                    cacheCreation = val;
                }
            }

            // 2. 匹配 OpenAI 格式
            if (prompt == 0)
            {
                var pMatches = System.Text.RegularExpressions.Regex.Matches(responseText, @"""prompt_tokens""\s*:\s*(\d+)");
                foreach (System.Text.RegularExpressions.Match m in pMatches)
                {
                    if (long.TryParse(m.Groups[1].Value, out var val) && val > prompt)
                    {
                        prompt = val;
                    }
                }
            }

            if (completion == 0)
            {
                var cMatches = System.Text.RegularExpressions.Regex.Matches(responseText, @"""completion_tokens""\s*:\s*(\d+)");
                foreach (System.Text.RegularExpressions.Match m in cMatches)
                {
                    if (long.TryParse(m.Groups[1].Value, out var val) && val > completion)
                    {
                        completion = val;
                    }
                }
            }

            if (cacheRead == 0)
            {
                var cachedMatches = System.Text.RegularExpressions.Regex.Matches(responseText, @"""cached_tokens""\s*:\s*(\d+)");
                foreach (System.Text.RegularExpressions.Match m in cachedMatches)
                {
                    if (long.TryParse(m.Groups[1].Value, out var val) && val > cacheRead)
                    {
                        cacheRead = val;
                    }
                }
            }
        }
        catch { }

        // 3. 服务端未返回 usage 时的估算保底（1 token ≈ 4 字节）
        if (prompt <= 0 && requestBodyLength > 0 && cacheRead <= 0)
        {
            prompt = Math.Max(1, requestBodyLength / 4);
        }
        if (completion <= 0 && responseLength > 0)
        {
            completion = Math.Max(1, responseLength / 4);
        }

        return (prompt, completion, cacheRead, cacheCreation);
    }

    private static async Task WriteFullResponseAsync(HttpContext context, HttpResponseMessage upstreamResponse, byte[] content)
    {
        context.Response.StatusCode = (int)upstreamResponse.StatusCode;
        foreach (var header in upstreamResponse.Headers)
        {
            if (!HopByHopHeaders.Contains(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }
        foreach (var header in upstreamResponse.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        await context.Response.Body.WriteAsync(content, context.RequestAborted);
    }

    /// <summary>
    /// 重写 JSON 请求体中的 model 字段值，实现透明模型别名映射
    /// </summary>
    private static byte[] RewriteModelInRequestBody(byte[] jsonBytes, string newModel)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonBytes);
            using var memoryStream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(memoryStream))
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.NameEquals("model"))
                    {
                        writer.WriteString("model", newModel);
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }
            return memoryStream.ToArray();
        }
        catch
        {
            // NOTE: 遇到非标准 JSON 时优雅降级，返回原始字节
            return jsonBytes;
        }
    }
}
