namespace App.Core.Responses;

/// <summary>
/// 分页结果模型（与 AGENTS.md 第 4.3 节分页契约一致）
/// </summary>
/// <typeparam name="T">列表项类型</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>当前页数据</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>总条数</summary>
    public int Total { get; init; }

    /// <summary>当前页码（从 1 起）</summary>
    public int Page { get; init; }

    /// <summary>每页条数</summary>
    public int PageSize { get; init; }
}
