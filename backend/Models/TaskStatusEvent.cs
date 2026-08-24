namespace ModelFailoverGateway.Models;

/// <summary>
/// 实时任务流式状态事件（用于驱动桌面宠物打字/思考/完成状态）
/// </summary>
public class TaskStatusEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string State { get; set; } = "idle"; // idle, thinking, completed, failover
    public string Model { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public long DurationMs { get; set; } = 0;
    public long TotalTokens { get; set; } = 0;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
