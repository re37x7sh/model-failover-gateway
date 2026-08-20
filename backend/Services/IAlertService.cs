using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 渠道异常通知服务接口
/// </summary>
public interface IAlertService
{
    void AddAlert(string channelId, string channelName, string group, string reason);
    List<ChannelAlert> GetActiveAlerts();
    bool DismissAlert(string alertId);
    void ClearAllAlerts();
}
