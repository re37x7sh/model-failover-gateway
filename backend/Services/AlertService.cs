using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows.Forms;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 渠道异常与任务完成主动通知服务实现
/// </summary>
public class AlertService : IAlertService
{
    private readonly List<ChannelAlert> _alerts = new();
    private readonly object _lock = new();
    private readonly ILogger<AlertService> _logger;
    private readonly TrayIconManager _trayManager;
    private readonly string _settingsFilePath;
    private TaskNotificationSettings _settings = new();

    private TaskStatusEvent _currentTaskStatus = new() { State = "idle" };

    public AlertService(ILogger<AlertService> logger, TrayIconManager trayManager)
    {
        _logger = logger;
        _trayManager = trayManager;

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
        _settingsFilePath = Path.Combine(dataDir, "notification_settings.json");

        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<TaskNotificationSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                    _trayManager.IsTaskCompleteNotificationEnabled = _settings.EnableBalloon;
                    _trayManager.IsSoundEnabled = _settings.EnableSound;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载通知设置文件失败");
        }
    }

    public TaskNotificationSettings GetNotificationSettings()
    {
        lock (_lock)
        {
            return _settings;
        }
    }

    public void SaveNotificationSettings(TaskNotificationSettings settings)
    {
        lock (_lock)
        {
            _settings = settings;
            _trayManager.IsTaskCompleteNotificationEnabled = settings.EnableBalloon;
            _trayManager.IsSoundEnabled = settings.EnableSound;

            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存通知设置文件失败");
            }
        }
    }

    public void NotifyTaskStart(string model, string channelName)
    {
        lock (_lock)
        {
            _currentTaskStatus = new TaskStatusEvent
            {
                State = "thinking",
                Model = model,
                ChannelName = channelName,
                Timestamp = DateTime.Now
            };
        }
    }

    public void NotifyTaskComplete(string model, string channelName, long durationMs, long tokens)
    {
        lock (_lock)
        {
            _currentTaskStatus = new TaskStatusEvent
            {
                State = "completed",
                Model = model,
                ChannelName = channelName,
                DurationMs = durationMs,
                TotalTokens = tokens,
                Timestamp = DateTime.Now
            };

            var durationSec = durationMs / 1000.0;
            // 智能耗时阈值过滤（只有达到阈值时才触发主动系统提醒）
            if (_settings.EnableTaskCompleteNotification && durationSec >= _settings.TaskCompleteThresholdSeconds)
            {
                _trayManager.ShowTaskCompleteNotification(model, durationMs, tokens);
            }
        }
    }

    public void NotifyTaskFailover(string model, string channelName, string reason)
    {
        lock (_lock)
        {
            _currentTaskStatus = new TaskStatusEvent
            {
                State = "failover",
                Model = model,
                ChannelName = channelName,
                Message = reason,
                Timestamp = DateTime.Now
            };
        }
    }

    public TaskStatusEvent GetCurrentTaskStatus()
    {
        lock (_lock)
        {
            // 如果上一个 completed/failover 事件超过 15 秒，自动回退到 idle 状态
            if ((_currentTaskStatus.State == "completed" || _currentTaskStatus.State == "failover") &&
                (DateTime.Now - _currentTaskStatus.Timestamp).TotalSeconds > 15)
            {
                _currentTaskStatus.State = "idle";
            }
            return _currentTaskStatus;
        }
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
