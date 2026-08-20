using System.Collections.Concurrent;
using System.Windows.Forms;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 渠道异常与故障转移告警通知服务实现
/// </summary>
public class AlertService : IAlertService
{
    private readonly List<ChannelAlert> _alerts = new();
    private readonly object _lock = new();
    private readonly ILogger<AlertService> _logger;
    private readonly TrayIconManager _trayManager;

    public AlertService(ILogger<AlertService> logger, TrayIconManager trayManager)
    {
        _logger = logger;
        _trayManager = trayManager;
    }

    /// <summary>
    /// 添加一条渠道异常告警，并防抖合并
    /// </summary>
    public void AddAlert(string channelId, string channelName, string group, string reason)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            channelName = "未知渠道";
        }

        var cleanReason = string.IsNullOrWhiteSpace(reason) ? "上游连接超时或异常" : reason;

        ChannelAlert? alertToNotify = null;

        lock (_lock)
        {
            // 防抖检查：如果最近 30 秒内已有该渠道同原因未关闭的告警，则合并频次
            var existing = _alerts.FirstOrDefault(a => 
                !a.IsDismissed && 
                a.ChannelId == channelId && 
                (DateTime.Now - a.Timestamp).TotalSeconds < 30);

            if (existing != null)
            {
                existing.OccurCount++;
                existing.Timestamp = DateTime.Now;
                existing.Reason = cleanReason;
                alertToNotify = existing;
            }
            else
            {
                var newAlert = new ChannelAlert
                {
                    ChannelId = channelId,
                    ChannelName = channelName,
                    Group = group ?? "all",
                    Reason = cleanReason,
                    Timestamp = DateTime.Now,
                    OccurCount = 1
                };

                _alerts.Insert(0, newAlert);

                // 最多保留 100 条通知
                if (_alerts.Count > 100)
                {
                    _alerts.RemoveAt(_alerts.Count - 1);
                }

                alertToNotify = newAlert;
            }
        }

        _logger.LogWarning("⚠️ 记录渠道异常通知: [{Channel}] - {Reason}", channelName, cleanReason);

        // Windows 系统托盘气泡通知
        try
        {
            _trayManager.ShowBalloonNotification(
                $"⚠️ 渠道异常告警: {channelName}",
                $"原因: {cleanReason}\n网关已触发自动故障转移",
                ToolTipIcon.Warning
            );
        }
        catch
        {
            // 忽略非 Windows 环境下的托盘报错
        }
    }

    /// <summary>
    /// 获取当前未手动关闭的活跃通知列表
    /// </summary>
    public List<ChannelAlert> GetActiveAlerts()
    {
        lock (_lock)
        {
            return _alerts
                .Where(a => !a.IsDismissed)
                .OrderByDescending(a => a.Timestamp)
                .ToList();
        }
    }

    /// <summary>
    /// 手动关闭单条通知
    /// </summary>
    public bool DismissAlert(string alertId)
    {
        lock (_lock)
        {
            var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
            if (alert != null)
            {
                alert.IsDismissed = true;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 一键清空/关闭所有通知
    /// </summary>
    public void ClearAllAlerts()
    {
        lock (_lock)
        {
            foreach (var alert in _alerts)
            {
                alert.IsDismissed = true;
            }
        }
    }
}
