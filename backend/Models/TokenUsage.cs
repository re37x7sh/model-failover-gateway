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
/// Token 全局汇总统计 DTO
/// </summary>
public class TokenStatsSummaryDto
{
    public long TotalTokens { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TodayTokens { get; set; }
    public long TotalRequests { get; set; }
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
    public long RequestCount { get; set; }
    public DateTime? LastUsed { get; set; }
}
