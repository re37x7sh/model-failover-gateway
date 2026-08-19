namespace ModelFailoverGateway.Models;

/// <summary>
/// 请求日志持久化与自动清理策略配置
/// </summary>
public class LogSettings
{
    /// <summary>
    /// 日志保留天数（默认 7 天，0 代表不限天数）
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// 最大保存日志条数（默认 2000 条）
    /// </summary>
    public int MaxCapacity { get; set; } = 2000;

    /// <summary>
    /// 是否开启定时自动清理
    /// </summary>
    public bool AutoCleanupEnabled { get; set; } = true;
}
