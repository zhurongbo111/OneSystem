using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 发票跨表只读查询仓储接口（实现见 App.Infrastructure）：可开票单据候选 + 已开票金额聚合。
/// 跨四张单据表的只读聚合不属于任何单一单据仓储，故按「查询职责」独立成接口；写侧仍由各单据仓储负责
/// （specs/032-erp-invoice/design.md §3.1）。
/// </summary>
public interface IInvoiceQueryRepository
{
    /// <summary>
    /// 查询某往来单位在指定发票方向下的可开票单据（未开票金额 &gt; 0 且未作废），按业务日期倒序。
    /// 进项 → 采购入库单 + 采购退货单；销项 → 销售出库单 + 销售退货单；四表查询后在内存合并分页
    /// </summary>
    /// <param name="partnerId">往来单位 id</param>
    /// <param name="type">发票类型（决定可开票的单据类型集合）</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<InvoicableOrderItem> Items, int Total)> GetInvoicableAsync(
        Guid partnerId,
        InvoiceType type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询某单据的已开票金额（所属发票未作废的明细聚合，见 design.md §0.3）
    /// </summary>
    /// <param name="orderType">被开票单据类型</param>
    /// <param name="orderId">被开票单据 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal> GetInvoicedAmountAsync(SettlementOrderType orderType, Guid orderId, CancellationToken cancellationToken = default);
}