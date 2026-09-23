using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Reports.ExportInventoryFlow;

/// <summary>
/// 进销存报表导出请求（erp-export）：筛选参数与报表查询一致；
/// 分页参数仅为与报表请求同形，导出取「当前筛选全量」，不参与分页（specs/027-erp-export/design.md §0.2）
/// </summary>
public sealed class ExportInventoryFlowRequest : IRequest<ExportResultDto>
{
    /// <summary>期间起（含，UTC），必填</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>期间止（不含，UTC），必填</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>商品 id，可空</summary>
    public Guid? ProductId { get; init; }

    /// <summary>分类 id，可空</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>是否只看期间有变动的商品</summary>
    public bool OnlyChanged { get; init; }

    /// <summary>仓库 id，可空（038；不传 = 全部仓合并）</summary>
    public Guid? WarehouseId { get; init; }

    /// <summary>页码，从 1 起（导出忽略，仅为与报表参数一致）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（导出忽略，仅为与报表参数一致）</summary>
    public int PageSize { get; init; } = 20;
}
