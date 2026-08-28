namespace ModelFailoverGateway.Models;

/// <summary>
/// 通用分页数据封装模型
/// </summary>
/// <typeparam name="T">列表项数据类型</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// 当前页的数据列表
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// 符合筛选条件的总记录数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 当前页码 (从 1 开始)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页记录数
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// 计算总页数
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
