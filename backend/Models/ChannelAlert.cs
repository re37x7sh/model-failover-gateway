namespace ModelFailoverGateway.Models;

/// <summary>
/// 渠道异常与故障转移通知实体
/// </summary>
public class ChannelAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string Group { get; set; } = "all";
    public string Reason { get; set; } = string.Empty;
    public int OccurCount { get; set; } = 1;
    public bool IsDismissed { get; set; } = false;
}
