using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetInventoryFlow;

/// <summary>
/// 进销存报表查询请求（Query 参数绑定；期间为左闭右开区间，明细口径见 design.md §0.1）。
/// </summary>
public sealed class GetInventoryFlowRequest : IRequest<ReportPageDto<InventoryFlowItemDto, InventoryFlowSummaryDto>>
{
    /// <summary>期间起（含，UTC），必填</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>期间止（不含，UTC），必填</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>商品 id，可空</summary>
    public Guid? ProductId { get; init; }

    /// <summary>分类 id，可空</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>是否只看期间有变动的商品（默认为否：展示筛选范围内全部启用商品）</summary>
    public bool OnlyChanged { get; init; }

    /// <summary>仓库 id，可空（038；不传 = 全部仓合并）</summary>
    public Guid? WarehouseId { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
