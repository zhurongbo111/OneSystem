namespace App.Core.Features.Reports;

/// <summary>
/// 报表分页出参包装：在统一分页字段（items / total / page / pageSize）之上追加 <see cref="Summary"/> 合计字段。
/// 合计为**全量筛选结果**口径（非当前页），故不并入全局 <c>PagedResult&lt;T&gt;</c>，避免污染分页契约；
/// 该类型仅本规格使用（specs/025-erp-report/design.md §3.3）。
/// </summary>
/// <typeparam name="TItem">报表行类型</typeparam>
/// <typeparam name="TSummary">合计模型类型</typeparam>
public sealed class ReportPageDto<TItem, TSummary>
{
    /// <summary>当前页数据</summary>
    public IReadOnlyList<TItem> Items { get; init; } = [];

    /// <summary>总条数（筛选结果全量）</summary>
    public int Total { get; init; }

    /// <summary>当前页码（从 1 起）</summary>
    public int Page { get; init; }

    /// <summary>每页条数</summary>
    public int PageSize { get; init; }

    /// <summary>合计（全量筛选结果口径，与当前页无关）</summary>
    public required TSummary Summary { get; init; }
}
