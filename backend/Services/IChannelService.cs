using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 渠道持久化与状态管理服务接口
/// </summary>
public interface IChannelService
{
    Task<List<Channel>> GetAllAsync();
    Task<Channel?> GetByIdAsync(string id);
    Task<Channel> CreateAsync(Channel channel);
    Task<Channel?> UpdateAsync(Channel channel);
    Task<bool> DeleteAsync(string id);
    Task<bool> ToggleAsync(string id, bool isEnabled);
    Task<bool> ReorderAsync(List<string> orderedIds);
    Task<ChannelTestResult> TestChannelAsync(Channel channel);
    Task MarkFailureAsync(string channelId, string reason);
    Task MarkSuccessAsync(string channelId);
}
