using Microsoft.AspNetCore.Mvc;
using ModelFailoverGateway.Models;
using ModelFailoverGateway.Services;

namespace ModelFailoverGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly TrayIconManager _trayManager;

    public NotificationsController(IAlertService alertService, TrayIconManager trayManager)
    {
        _alertService = alertService;
        _trayManager = trayManager;
    }

    /// <summary>
    /// 获取当前所有活跃的未关闭通知
    /// </summary>
    [HttpGet]
    public IActionResult GetActiveNotifications()
    {
        var alerts = _alertService.GetActiveAlerts();
        return Ok(new
        {
            success = true,
            total = alerts.Count,
            data = alerts
        });
    }

    /// <summary>
    /// 手动关闭指定的一条通知
    /// </summary>
    [HttpPost("{id}/dismiss")]
    public IActionResult DismissNotification(string id)
    {
        var res = _alertService.DismissAlert(id);
        return Ok(new
        {
            success = res,
            message = res ? "通知已关闭" : "通知不存在或已关闭"
        });
    }

    /// <summary>
    /// 一键清空/关闭所有通知
    /// </summary>
    [HttpPost("clear")]
    public IActionResult ClearAllNotifications()
    {
        _alertService.ClearAllAlerts();
        return Ok(new
        {
            success = true,
            message = "所有通知已清空"
        });
    }

    /// <summary>
    /// 获取长任务完成提醒与桌面宠物配置
    /// </summary>
    [HttpGet("settings")]
    public IActionResult GetNotificationSettings()
    {
        var settings = _alertService.GetNotificationSettings();
        return Ok(new
        {
            success = true,
            data = settings
        });
    }

    /// <summary>
    /// 保存长任务完成提醒与桌面宠物配置
    /// </summary>
    [HttpPost("settings")]
    public IActionResult SaveNotificationSettings([FromBody] TaskNotificationSettings settings)
    {
        _alertService.SaveNotificationSettings(settings);
        return Ok(new
        {
            success = true,
            message = "提醒与宠物设置保存成功"
        });
    }

    /// <summary>
    /// 获取当前 AI 任务流式实时状态（用于驱动桌面宠物打字/思考/撒花）
    /// </summary>
    [HttpGet("task-status")]
    public IActionResult GetTaskStatus()
    {
        var status = _alertService.GetCurrentTaskStatus();
        return Ok(new
        {
            success = true,
            data = status
        });
    }

    /// <summary>
    /// 测试播放提示音
    /// </summary>
    [HttpPost("test-chime")]
    public IActionResult TestChime()
    {
        _trayManager.PlayChimeSound();
        return Ok(new
        {
            success = true,
            message = "已播放测试音效"
        });
    }
}
