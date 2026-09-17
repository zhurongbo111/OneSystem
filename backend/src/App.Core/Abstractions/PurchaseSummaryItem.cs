namespace App.Core.Abstractions;

/// <summary>
/// 采购汇总行读模型（按往来单位或商品维度聚合；金额为明细小计合计，退货与入库分列，净额由 Mapper 计算）。
/// 口径见 specs/025-erp-report/design.md §0.1「金额口径」。
/// </summary>
public sealed record PurchaseSummaryItem
{
    /// <summary>分组键（往来单位 id 或商品 id）</summary>
    public required Guid? Key { get; init; }

    /// <summary>分组名称（往来单位名称快照或商品名称快照）</summary>
    public required string Name { get; init; }

    /// <summary>计量单位（仅商品维度有值；往来维度为 null）</summary>
    public string? Unit { get; init; }

    /// <summary>入库单数（未作废的单据去重计数；商品维度为含该商品的单据数）</summary>
    public required int OrderCount { get; init; }

    /// <summary>入库数量（Σ 明细数量）</summary>
    public required int InboundQuantity { get; init; }

    /// <summary>入库金额（Σ 明细小计）</summary>
    public required decimal InboundAmount { get; init; }

    /// <summary>退货数量（Σ 采购退货明细数量，无退货为 0）</summary>
    public required int ReturnQuantity { get; init; }

    /// <summary>退货金额（Σ 采购退货明细小计，无退货为 0）</summary>
    public required decimal ReturnAmount { get; init; }
}
