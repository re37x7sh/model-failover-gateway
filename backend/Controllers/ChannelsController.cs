using Microsoft.AspNetCore.Mvc;
using ModelFailoverGateway.Models;
using ModelFailoverGateway.Services;

namespace ModelFailoverGateway.Controllers;

/// <summary>
/// 渠道配置与连通性管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChannelsController : ControllerBase
{
    private readonly IChannelService _channelService;

    public ChannelsController(IChannelService channelService)
    {
        _channelService = channelService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Channel>>>> GetAll()
    {
        var channels = await _channelService.GetAllAsync();
        return Ok(ApiResponse<List<Channel>>.Ok(channels));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Channel>>> GetById(string id)
    {
        var channel = await _channelService.GetByIdAsync(id);
        if (channel == null)
        {
            return NotFound(ApiResponse<Channel>.Fail("未找到指定渠道"));
        }
        return Ok(ApiResponse<Channel>.Ok(channel));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Channel>>> Create([FromBody] Channel channel)
    {
        if (string.IsNullOrWhiteSpace(channel.Name) || string.IsNullOrWhiteSpace(channel.BaseUrl))
        {
            return BadRequest(ApiResponse<Channel>.Fail("渠道名称和 BaseUrl 不能为空"));
        }

        var created = await _channelService.CreateAsync(channel);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<Channel>.Ok(created, "渠道创建成功"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<Channel>>> Update(string id, [FromBody] Channel channel)
    {
        channel.Id = id;
        var updated = await _channelService.UpdateAsync(channel);
        if (updated == null)
        {
            return NotFound(ApiResponse<Channel>.Fail("未找到要更新的渠道"));
        }
        return Ok(ApiResponse<Channel>.Ok(updated, "渠道更新成功"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(string id)
    {
        var success = await _channelService.DeleteAsync(id);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.Fail("未找到要删除的渠道"));
        }
        return Ok(ApiResponse<bool>.Ok(true, "渠道已删除"));
    }

    [HttpPost("{id}/toggle")]
    public async Task<ActionResult<ApiResponse<bool>>> Toggle(string id, [FromBody] ToggleRequest request)
    {
        var success = await _channelService.ToggleAsync(id, request.IsEnabled);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.Fail("未找到要切换状态的渠道"));
        }
        return Ok(ApiResponse<bool>.Ok(true, request.IsEnabled ? "渠道已启用" : "渠道已禁用"));
    }

    [HttpPost("reorder")]
    public async Task<ActionResult<ApiResponse<bool>>> Reorder([FromBody] List<string> orderedIds)
    {
        var success = await _channelService.ReorderAsync(orderedIds);
        return Ok(ApiResponse<bool>.Ok(success, "优先级调整成功"));
    }

    [HttpPost("test")]
    public async Task<ActionResult<ApiResponse<ChannelTestResult>>> Test([FromBody] Channel channel)
    {
        var result = await _channelService.TestChannelAsync(channel);
        return Ok(ApiResponse<ChannelTestResult>.Ok(result));
    }
}

public class ToggleRequest
{
    public bool IsEnabled { get; set; }
}
