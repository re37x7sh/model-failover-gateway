namespace ModelFailoverGateway.Services;

/// <summary>
/// 代理转发与故障转移引擎接口
/// </summary>
public interface IProxyEngine
{
    Task ForwardRequestAsync(HttpContext context);
}
