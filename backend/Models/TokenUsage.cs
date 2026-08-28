namespace ModelFailoverGateway.Models;

/// <summary>
/// Token 消耗记录实体
/// </summary>
public class TokenUsageRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string Group { get; set; } = "all";
    public string MaskedKey { get; set; } = string.Empty;
    public string? Model { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens => PromptTokens + CompletionTokens;
    public bool IsStream { get; set; }
}

/// <summary>
/// Token 全局汇总统计 DTO（包含真实费用折算）
/// </summary>
public class TokenStatsSummaryDto
{
    public long TotalTokens { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TodayTokens { get; set; }
    public long TotalRequests { get; set; }
    public double TotalCostUsd { get; set; }
    public double TotalCostCny { get; set; }
    public double TodayCostUsd { get; set; }
    public double TodayCostCny { get; set; }
}

/// <summary>
/// 渠道维度 Token 消耗统计 DTO
/// </summary>
public class ChannelTokenStatsDto
{
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string Group { get; set; } = "all";
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens => PromptTokens + CompletionTokens;
    public double CostUsd { get; set; }
    public double CostCny { get; set; }
    public long RequestCount { get; set; }
    public int KeyCount { get; set; }
    public DateTime? LastUsed { get; set; }
}

/// <summary>
/// 渠道 + Key 细分维度 Token 消耗统计 DTO
/// </summary>
public class KeyTokenStatsDto
{
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string Group { get; set; } = "all";
    public string MaskedKey { get; set; } = string.Empty;
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens => PromptTokens + CompletionTokens;
    public double CostUsd { get; set; }
    public double CostCny { get; set; }
    public long RequestCount { get; set; }
    public DateTime? LastUsed { get; set; }
}

/// <summary>
/// 按日聚合趋势统计 DTO
/// </summary>
public class DailyTokenStatsDto
{
    public string Date { get; set; } = string.Empty; // yyyy-MM-dd
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens => PromptTokens + CompletionTokens;
    public double CostUsd { get; set; }
    public double CostCny { get; set; }
    public long RequestCount { get; set; }
}

/// <summary>
/// 模型分布统计 DTO
/// </summary>
public class ModelTokenStatsDto
{
    public string Model { get; set; } = string.Empty;
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens => PromptTokens + CompletionTokens;
    public double CostUsd { get; set; }
    public double CostCny { get; set; }
    public long RequestCount { get; set; }
    public double Percentage { get; set; }
}
