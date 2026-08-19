using Microsoft.AspNetCore.Mvc;
using ModelFailoverGateway.Models;
using ModelFailoverGateway.Services;

namespace ModelFailoverGateway.Controllers;

/// <summary>
/// 系统设置、配置自动接管与系统日志控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IConfigInjectionService _configService;
    private readonly ISystemLogService _systemLogService;
    private readonly IConfiguration _configuration;

    public SystemController(
        IConfigInjectionService configService,
        ISystemLogService systemLogService,
        IConfiguration configuration)
    {
        _configService = configService;
        _systemLogService = systemLogService;
        _configuration = configuration;
    }

    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<SystemStatusDto>>> GetStatus()
    {
        var currentPort = _configService.GetConfiguredPort();
        var status = await _configService.GetStatusAsync(currentPort);
        return Ok(ApiResponse<SystemStatusDto>.Ok(status));
    }

    [HttpPost("inject")]
    public async Task<ActionResult<ApiResponse<bool>>> Inject([FromBody] InjectRequest request)
    {
        var port = request.Port > 0 ? request.Port : _configService.GetConfiguredPort();
        var group = string.IsNullOrWhiteSpace(request.Group) ? "claude" : request.Group.Trim();
        var providerName = string.IsNullOrWhiteSpace(request.ProviderName) ? "gateway" : request.ProviderName.Trim();
        var success = await _configService.InjectAsync(request.Target, port, group, providerName);

        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Fail("部分或全部客户端配置接管失败，请查看系统日志"));
        }

        return Ok(ApiResponse<bool>.Ok(true, "客户端配置已成功接管"));
    }

    [HttpPost("restore")]
    public async Task<ActionResult<ApiResponse<bool>>> Restore([FromBody] RestoreRequest request)
    {
        var success = await _configService.RestoreAsync(request.Target);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Fail("部分客户端配置还原失败或未找到备份"));
        }

        return Ok(ApiResponse<bool>.Ok(true, "客户端配置已成功还原"));
    }

    [HttpPost("port")]
    public async Task<ActionResult<ApiResponse<bool>>> SetPort([FromBody] PortRequest request)
    {
        if (request.Port < 1024 || request.Port > 65535)
        {
            return BadRequest(ApiResponse<bool>.Fail("端口号必须在 1024 到 65535 之间"));
        }

        var success = await _configService.SavePortConfigAsync(request.Port);
        if (!success)
        {
            return StatusCode(500, ApiResponse<bool>.Fail("保存端口配置失败"));
        }

        return Ok(ApiResponse<bool>.Ok(true, $"端口已更新为 {request.Port}，重启网关后生效"));
    }

    [HttpGet("logs")]
    public ActionResult<ApiResponse<List<SystemLogEntry>>> GetSystemLogs([FromQuery] int limit = 200, [FromQuery] string? level = null)
    {
        var logs = _systemLogService.GetRecentLogs(limit, level);
        return Ok(ApiResponse<List<SystemLogEntry>>.Ok(logs));
    }

    [HttpDelete("logs")]
    public ActionResult<ApiResponse<bool>> ClearSystemLogs()
    {
        _systemLogService.ClearLogs();
        return Ok(ApiResponse<bool>.Ok(true, "系统日志已清空"));
    }
}

public class InjectRequest
{
    public string Target { get; set; } = "all";
    public int Port { get; set; }
    public string Group { get; set; } = "claude";
    public string ProviderName { get; set; } = "gateway";
}

public class RestoreRequest
{
    public string Target { get; set; } = "all";
}

public class PortRequest
{
    public int Port { get; set; }
}
