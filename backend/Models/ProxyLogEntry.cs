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
