namespace ModelFailoverGateway.Models;

/// <summary>
/// 请求与故障转移日志实体
/// </summary>
public class ProxyLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ClientIp { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = "POST";
    public string RequestPath { get; set; } = string.Empty;
    public string? Model { get; set; }
    public List<string> TriedChannels { get; set; } = new();
    public string? FinalChannel { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public bool IsFailover { get; set; }
    public string? ErrorDetails { get; set; }
    public Dictionary<string, string>? RequestHeaders { get; set; }

    /// <summary>
    /// 请求运行状态：PENDING（进行中）、SUCCESS（成功完成）、FAILED（失败/异常）
    /// </summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// 客户端请求体载荷内容（支持查看完整 Prompt/Messages/Input，超限安全截断）
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// 响应内容摘要或流式错误提取内容
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// 输入 Prompt 消耗的 Token 数
    /// </summary>
    public long? PromptTokens { get; set; }

    /// <summary>
    /// 输出 Completion 消耗的 Token 数
    /// </summary>
    public long? CompletionTokens { get; set; }
}

/// <summary>
/// 统一 API 响应包装
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T? data, string message = "操作成功")
    {
        return new ApiResponse<T> { Success = true, Message = message, Data = data };
    }

    public static ApiResponse<T> Fail(string message)
    {
        return new ApiResponse<T> { Success = false, Message = message, Data = default };
    }
}

/// <summary>
/// 仪表盘聚合数据模型
/// </summary>
public class DashboardSummary
{
    public int TotalChannels { get; set; }
    public int ActiveChannels { get; set; }
    public long TotalRequests { get; set; }
    public long TotalFailovers { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double SuccessRate => TotalRequests == 0 ? 100.0 : Math.Round((double)SuccessfulRequests / TotalRequests * 100, 2);
    public string? CurrentPrimaryChannelName { get; set; }
}
