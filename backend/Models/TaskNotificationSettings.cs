namespace ModelFailoverGateway.Models;

/// <summary>
/// 长任务主动提醒与桌面宠物配置实体
/// </summary>
public class TaskNotificationSettings
{
    /// <summary>
    /// 是否开启长任务完成提醒
    /// </summary>
    public bool EnableTaskCompleteNotification { get; set; } = true;

    /// <summary>
    /// 触发提醒的耗时阈值（秒），默认 5 秒
    /// </summary>
    public int TaskCompleteThresholdSeconds { get; set; } = 5;

    /// <summary>
    /// 是否播放提示音
    /// </summary>
    public bool EnableSound { get; set; } = true;

    /// <summary>
    /// 是否弹出系统气泡通知
    /// </summary>
    public bool EnableBalloon { get; set; } = true;

    /// <summary>
    /// 是否启用桌面萌宠挂件
    /// </summary>
    public bool EnableDesktopPet { get; set; } = true;

    /// <summary>
    /// 宠物形象选择 (cat, robot, dog)
    /// </summary>
    public string PetAvatar { get; set; } = "cat";
}
