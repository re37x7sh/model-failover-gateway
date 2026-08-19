using Microsoft.AspNetCore.Mvc;
using ModelFailoverGateway.Models;
using ModelFailoverGateway.Services;

namespace ModelFailoverGateway.Controllers;

/// <summary>
/// 请求日志与统计信息控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly IChannelService _channelService;

    public LogsController(ILogService logService, IChannelService channelService)
    {
        _logService = logService;
        _channelService = channelService;
    }

    [HttpGet]
    public ActionResult<ApiResponse<List<ProxyLogEntry>>> GetLogs([FromQuery] int limit = 100)
    {
        var logs = _logService.GetRecentLogs(limit);
        return Ok(ApiResponse<List<ProxyLogEntry>>.Ok(logs));
    }

    [HttpDelete]
    public ActionResult<ApiResponse<bool>> ClearLogs()
    {
        _logService.ClearLogs();
        return Ok(ApiResponse<bool>.Ok(true, "日志已清空"));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummary>>> GetSummary()
    {
        var channels = await _channelService.GetAllAsync();
        var total = channels.Count;
        var active = channels.Count(c => c.IsEnabled);
        var primaryName = channels.FirstOrDefault(c => c.IsEnabled)?.Name;

        var summary = _logService.GetDashboardSummary(total, active, primaryName);
        return Ok(ApiResponse<DashboardSummary>.Ok(summary));
    }

    [HttpGet("settings")]
    public ActionResult<ApiResponse<LogSettings>> GetSettings()
    {
        var settings = _logService.GetSettings();
        return Ok(ApiResponse<LogSettings>.Ok(settings));
    }

    [HttpPost("settings")]
    public ActionResult<ApiResponse<bool>> SaveSettings([FromBody] LogSettings settings)
    {
        if (settings.MaxCapacity < 100 || settings.MaxCapacity > 50000)
        {
            return BadRequest(ApiResponse<bool>.Fail("最大容量限制应在 100 到 50000 条之间"));
        }
        if (settings.RetentionDays < 0 || settings.RetentionDays > 365)
        {
            return BadRequest(ApiResponse<bool>.Fail("保留天数必须在 0 到 365 天之间（0 表示不限）"));
        }

        _logService.SaveSettings(settings);
        return Ok(ApiResponse<bool>.Ok(true, "日志清理策略已保存并生效"));
    }
}
