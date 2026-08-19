using Microsoft.AspNetCore.Mvc;
using ModelFailoverGateway.Models;
using ModelFailoverGateway.Services;

namespace ModelFailoverGateway.Controllers;

/// <summary>
/// Token 消耗多维统计控制器
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

    [HttpDelete]
    public ActionResult<ApiResponse<bool>> ClearStats()
    {
        _tokenStatsService.ClearStats();
        return Ok(ApiResponse<bool>.Ok(true, "Token 统计数据已清空"));
    }
}
