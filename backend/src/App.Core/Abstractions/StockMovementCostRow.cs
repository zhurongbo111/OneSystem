using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 成本重算用的流水行读模型（按 <c>CreatedAt, Id</c> 升序，仅 <c>Costs/RecalculateCosts</c> 使用）。
/// 关联单价为**左连接带出**（避免重算时 N+1 反查）：采购入库取采购单明细单价、期初建账取盘点明细成本单价；
/// 其余类型（出库 / 冲销）由重算过程按 specs/026-erp-cost/design.md §0.2 的来源表在内存中推演。
/// </summary>
public sealed record StockMovementCostRow
{
    /// <summary>流水 ID（重算写回成本列的主键）</summary>
    public required Guid Id { get; init; }

    /// <summary>变动商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>变动类型</summary>
    public required StockMovementType MovementType { get; init; }

    /// <summary>变动量（带符号）</summary>
    public required int Quantity { get; init; }

    /// <summary>来源单据 id（无来源单据时为 null）</summary>
    public Guid? SourceId { get; init; }

    /// <summary>来源单据号</summary>
    public string? SourceNo { get; init; }

    /// <summary>变动时间（重算的时序依据）</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>采购入库明细单价快照（<c>PurchaseInbound</c> 有效；其余类型为 null）</summary>
    public decimal? PurchaseUnitPrice { get; init; }

    /// <summary>期初建账成本单价（<c>InitialStock</c> 有效；其余类型为 null）</summary>
    public decimal? InitialUnitCost { get; init; }
}
