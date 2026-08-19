namespace ModelFailoverGateway.Models;

/// <summary>
/// 上游模型渠道配置实体
/// </summary>
public class Channel
{
    /// <summary>
    /// 渠道唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 渠道显示名称（如：主力中转A、官方Anthropic）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 上游基础地址（如：https://api.anthropic.com 或 https://api.relay.com/v1）
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 访问 API Key（支持配置多个，每行一个或逗号分隔）
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 获取解析后的有效 API Key 列表
    /// </summary>
    public List<string> GetApiKeys()
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) return new List<string>();
        return ApiKey.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(k => !string.IsNullOrWhiteSpace(k))
                     .Distinct()
                     .ToList();
    }

    /// <summary>
    /// 支持的模型名称匹配规则（逗号分隔或 * 表示全部）
    /// </summary>
    public string Models { get; set; } = "*";

    /// <summary>
    /// 所属客户端分组（如：all 通用、claude 仅Claude、codex 仅Codex、或自定义分组名）
    /// </summary>
    public string Group { get; set; } = "all";

    /// <summary>
    /// 模型别名映射规则（每行一条：sourceModel => targetModel，例如：claude-3-7-sonnet => gpt-5.6-sol）
    /// </summary>
    public string ModelMapping { get; set; } = string.Empty;

    /// <summary>
    /// 获取解析后的模型别名映射字典
    /// </summary>
    public Dictionary<string, string> GetModelMappings()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(ModelMapping)) return dict;

        var lines = ModelMapping.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(new[] { "=>", "->", ":" }, 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            {
                dict[parts[0]] = parts[1];
            }
        }
        return dict;
    }

    /// <summary>
    /// 根据映射规则转换模型名称
    /// </summary>
    public string GetMappedModel(string? sourceModel)
    {
        if (string.IsNullOrWhiteSpace(sourceModel)) return sourceModel ?? string.Empty;
        var mappings = GetModelMappings();
        if (mappings.TryGetValue(sourceModel, out var mapped))
        {
            return mapped;
        }
        if (mappings.TryGetValue("*", out var wildcardMapped))
        {
            return wildcardMapped;
        }
        return sourceModel;
    }

    /// <summary>
    /// 判断当前渠道是否匹配请求指定的分组
    /// </summary>
    public bool IsGroupMatch(string? requestedGroup)
    {
        if (string.IsNullOrWhiteSpace(requestedGroup) || requestedGroup.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (Group.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return Group.Equals(requestedGroup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 优先级序号（数值越小优先级越高，最先被尝试）
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 连续失败计数
    /// </summary>
    public int FailCount { get; set; } = 0;

    /// <summary>
    /// 最近一次失败原因
    /// </summary>
    public string? LastFailureReason { get; set; }

    /// <summary>
    /// 最近一次成功调用时间
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 渠道连通性与额度测试结果
/// </summary>
public class ChannelTestResult
{
    public bool Success { get; set; }
    public long LatencyMs { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ResponseSnippet { get; set; }
}
