using System.Text;
using Microsoft.AspNetCore.Mvc;
using ModelFailoverGateway.Models;
using ModelFailoverGateway.Services;

namespace ModelFailoverGateway.Controllers;

/// <summary>
/// Token 消耗多维统计与趋势分析控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TokenStatsController : ControllerBase
{
    private readonly ITokenStatsService _tokenStatsService;

    public TokenStatsController(ITokenStatsService tokenStatsService)
    {
        _tokenStatsService = tokenStatsService;
    }

    [HttpGet("summary")]
    public ActionResult<ApiResponse<TokenStatsSummaryDto>> GetSummary()
    {
        var summary = _tokenStatsService.GetSummary();
        return Ok(ApiResponse<TokenStatsSummaryDto>.Ok(summary));
    }

    [HttpGet("channels")]
    public ActionResult<ApiResponse<List<ChannelTokenStatsDto>>> GetChannelStats()
    {
        var list = _tokenStatsService.GetChannelStats();
        return Ok(ApiResponse<List<ChannelTokenStatsDto>>.Ok(list));
    }

    [HttpGet("keys")]
    public ActionResult<ApiResponse<List<KeyTokenStatsDto>>> GetKeyStats([FromQuery] string? channelId = null)
    {
        var list = _tokenStatsService.GetKeyStats(channelId);
        return Ok(ApiResponse<List<KeyTokenStatsDto>>.Ok(list));
    }

    [HttpGet("daily")]
    public ActionResult<ApiResponse<List<DailyTokenStatsDto>>> GetDailyStats([FromQuery] int days = 7)
    {
        var list = _tokenStatsService.GetDailyStats(days);
        return Ok(ApiResponse<List<DailyTokenStatsDto>>.Ok(list));
    }

    [HttpGet("models")]
    public ActionResult<ApiResponse<List<ModelTokenStatsDto>>> GetModelDistribution()
    {
        var list = _tokenStatsService.GetModelDistribution();
        return Ok(ApiResponse<List<ModelTokenStatsDto>>.Ok(list));
    }

    [HttpGet("export/csv")]
    public IActionResult ExportCsv()
    {
        var csvContent = _tokenStatsService.GenerateCsvExport();
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        var fileName = $"token_usage_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpDelete]
    public ActionResult<ApiResponse<bool>> ClearStats()
    {
        _tokenStatsService.ClearStats();
        return Ok(ApiResponse<bool>.Ok(true, "Token 统计数据已清空"));
    }
}
