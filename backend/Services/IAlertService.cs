using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 渠道异常与任务完成主动通知服务接口
/// </summary>
public interface IAlertService
{
    // 渠道异常通知
    void AddAlert(string channelId, string channelName, string group, string reason);
    List<ChannelAlert> GetActiveAlerts();
    bool DismissAlert(string alertId);
    void ClearAllAlerts();

    // 长任务提醒与状态联动
    TaskNotificationSettings GetNotificationSettings();
    void SaveNotificationSettings(TaskNotificationSettings settings);
    void NotifyTaskStart(string model, string channelName);
    void NotifyTaskComplete(string model, string channelName, long durationMs, long tokens);
    void NotifyTaskFailover(string model, string channelName, string reason);
    TaskStatusEvent GetCurrentTaskStatus();
}
