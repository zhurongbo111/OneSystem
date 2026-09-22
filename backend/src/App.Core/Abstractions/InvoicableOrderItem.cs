using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 可开票单据候选读模型（仓储出参契约，供新建发票页选择关联单据；不暴露到 API）。
/// </summary>
public sealed record InvoicableOrderItem
{
    /// <summary>被开票单据类型</summary>
    public required SettlementOrderType OrderType { get; init; }

    /// <summary>被开票单据 id</summary>
    public required Guid OrderId { get; init; }

    /// <summary>被开票单据号</summary>
    public required string OrderNo { get; init; }

    /// <summary>被开票单据业务日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>单据总额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>已开票金额（未作废发票的明细聚合）</summary>
    public required decimal InvoicedAmount { get; init; }

    /// <summary>未开票金额 = 总额 − 已开票金额（推导，不落列，见 design.md §0.3）</summary>
    public decimal UninvoicedAmount => TotalAmount - InvoicedAmount;
}