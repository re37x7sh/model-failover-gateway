using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 后台轻量健康探测与半开熔断恢复守护服务 (Active Health Check & Half-Open Recovery Background Service)
/// 定时检测处于智能熔断状态的渠道，在冷却期即将到期时主动嗅探上游可用性；
/// 探活成功则提前自动解除熔断并恢复健康状态，彻底杜绝让真实业务请求充当“探路兵”。
/// </summary>
public class HealthCheckBackgroundService : BackgroundService
{
    private readonly IChannelService _channelService;
    private readonly ILogger<HealthCheckBackgroundService> _logger;

    public HealthCheckBackgroundService(
        IChannelService channelService,
        ILogger<HealthCheckBackgroundService> logger)
    {
        _channelService = channelService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 主动探活与半开熔断恢复后台守护服务已启动，每 5 秒巡检一次熔断中渠道");

        // 启动延迟 3 秒，让主程序完成初始化
        await Task.Delay(3000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var candidates = await _channelService.GetChannelsNeedingProbeAsync();
                if (candidates.Count > 0)
                {
                    foreach (var channel in candidates)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        try
                        {
                            await _channelService.ProbeAndRecoverChannelAsync(channel, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "主动探活渠道 [{ChannelName}] 时发生未捕获异常", channel.Name);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "后台探活守护轮询巡检异常");
            }

            // 巡检间隔 5 秒
            await Task.Delay(5000, stoppingToken);
        }

        _logger.LogInformation("主动探活与半开熔断恢复后台守护服务已停止");
    }
}
