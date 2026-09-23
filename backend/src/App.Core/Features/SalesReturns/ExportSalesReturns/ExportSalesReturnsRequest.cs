using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Exports;

namespace App.Core.Features.SalesReturns.ExportSalesReturns;

/// <summary>
/// 销售退货单导出请求（erp-export）：筛选参数与列表查询一致；
/// 分页参数仅为与列表请求同形，导出取「当前筛选全量」，不参与分页（specs/027-erp-export/design.md §0.2）
/// </summary>
public sealed class ExportSalesReturnsRequest : IRequest<ExportResultDto>
{
    /// <summary>页码，从 1 起（导出忽略，仅为与列表参数一致）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（导出忽略，仅为与列表参数一致）</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>单号 / 客户名称关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>客户 id，可空</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>起始业务日期（含），可空</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束业务日期（含），可空</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>结算状态（0 未结算 / 1 部分结算 / 2 已结算，按已结金额推导），可空</summary>
    public SettlementState? SettlementState { get; init; }

    /// <summary>入库仓 id，可空（038；不传 = 全部仓）</summary>
    public Guid? WarehouseId { get; init; }
}
