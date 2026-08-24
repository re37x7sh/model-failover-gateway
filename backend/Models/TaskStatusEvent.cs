namespace ModelFailoverGateway.Models;

/// <summary>
/// 实时任务流式状态事件（用于驱动 Bongo Cat 打字/工具调用/完成状态）
/// </summary>
public class TaskStatusEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string State { get; set; } = "idle"; // idle, thinking, tool_use, completed, failover
    public string Model { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public int TurnCount { get; set; } = 1;
    public bool IsToolCall { get; set; } = false;
    public long DurationMs { get; set; } = 0; // 当前轮次耗时
    public long SessionDurationMs { get; set; } = 0; // 整个 Agent 会话链累计耗时
    public long TotalTokens { get; set; } = 0; // 当前轮次 Tokens
    public long SessionTotalTokens { get; set; } = 0; // 整个 Agent 会话链累计 Tokens
    public string Message { get; set; } = string.Empty;
    public string StepDescription { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public DateTime SessionStartTime { get; set; } = DateTime.Now;
}
