using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Exports;

namespace App.Core.Features.StockMovements.ExportStockMovements;

/// <summary>
/// 库存流水导出请求（erp-export）：筛选参数与列表查询一致；
/// 分页参数仅为与列表请求同形，导出取「当前筛选全量」，不参与分页（specs/027-erp-export/design.md §0.2）
/// </summary>
public sealed class ExportStockMovementsRequest : IRequest<ExportResultDto>
{
    /// <summary>来源单号关键词（模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>商品 id，可空</summary>
    public Guid? ProductId { get; init; }

    /// <summary>变动类型，可空</summary>
    public StockMovementType? Type { get; init; }

    /// <summary>变动时间起（含，UTC），可空</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>变动时间止（含，UTC），可空</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>页码，从 1 起（导出忽略，仅为与列表参数一致）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（导出忽略，仅为与列表参数一致）</summary>
    public int PageSize { get; init; } = 20;
}
