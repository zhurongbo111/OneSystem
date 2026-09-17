namespace App.Core.Abstractions;

/// <summary>
/// 销售汇总行读模型（按客户或商品维度聚合；金额为明细小计合计，退货与出库分列，净额由 Mapper 计算）。
/// 口径见 specs/025-erp-report/design.md §0.1「金额口径」（与采购汇总同构，替换为销售单据表）。
/// </summary>
public sealed record SalesSummaryItem
{
    /// <summary>分组键（客户 id 或商品 id）</summary>
    public required Guid? Key { get; init; }

    /// <summary>分组名称（客户名称快照或商品名称快照）</summary>
    public required string Name { get; init; }

    /// <summary>计量单位（仅商品维度有值；往来维度为 null）</summary>
    public string? Unit { get; init; }

    /// <summary>出库单数（未作废的单据去重计数；商品维度为含该商品的单据数）</summary>
    public required int OrderCount { get; init; }

    /// <summary>出库数量（Σ 明细数量）</summary>
    public required int OutboundQuantity { get; init; }

    /// <summary>出库金额（Σ 明细小计）</summary>
    public required decimal OutboundAmount { get; init; }

    /// <summary>退货数量（Σ 销售退货明细数量，无退货为 0）</summary>
    public required int ReturnQuantity { get; init; }

    /// <summary>退货金额（Σ 销售退货明细小计，无退货为 0）</summary>
    public required decimal ReturnAmount { get; init; }
}
