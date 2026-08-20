using Microsoft.AspNetCore.Mvc;
using ModelFailoverGateway.Models;
using ModelFailoverGateway.Services;

namespace ModelFailoverGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IAlertService _alertService;

    public NotificationsController(IAlertService alertService)
    {
        _alertService = alertService;
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
}
