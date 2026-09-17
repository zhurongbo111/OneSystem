using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 未结单据候选读模型（仓储出参契约，供新建收付款单页选择核销单据；不暴露到 API）。
/// </summary>
public sealed record SettlementCandidateItem
{
    /// <summary>被核销单据类型</summary>
    public required SettlementOrderType OrderType { get; init; }

    /// <summary>被核销单据 id</summary>
    public required Guid OrderId { get; init; }

    /// <summary>被核销单据号</summary>
    public required string OrderNo { get; init; }

    /// <summary>被核销单据业务日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>单据总额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>已结算金额</summary>
    public required decimal SettledAmount { get; init; }

    /// <summary>未结金额 = 总额 − 已结算金额（推导，不落列）</summary>
    public decimal UnsettledAmount => TotalAmount - SettledAmount;
}
