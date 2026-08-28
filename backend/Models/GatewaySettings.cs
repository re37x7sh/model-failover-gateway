namespace ModelFailoverGateway.Models;

/// <summary>
/// 网关全局安全与访问鉴权设置实体
/// </summary>
public class GatewaySettings
{
    /// <summary>
    /// 是否开启网关请求鉴权保护（开启后客户端必须携带有效的 Bearer / x-api-key）
    /// </summary>
    public bool RequireAuth { get; set; } = false;

    /// <summary>
    /// 网关自定义访问令牌（Gateway Token）
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// 渠道负载均衡与调度策略（priority 优先级主备、round_robin 轮询分流、random 随机分流）
    /// </summary>
    public string LoadBalancingStrategy { get; set; } = "priority";
}
