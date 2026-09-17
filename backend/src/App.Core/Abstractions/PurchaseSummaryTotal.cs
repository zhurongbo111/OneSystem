namespace App.Core.Abstractions;

/// <summary>
/// 采购汇总合计模型（对**全量筛选结果**聚合，与当前页无关；净额由 Mapper 在出参层计算）。
/// </summary>
public sealed record PurchaseSummaryTotal
{
    /// <summary>入库单数合计</summary>
    public required int OrderCount { get; init; }

    /// <summary>入库数量合计</summary>
    public required int InboundQuantity { get; init; }

    /// <summary>入库金额合计</summary>
    public required decimal InboundAmount { get; init; }

    /// <summary>退货数量合计</summary>
    public required int ReturnQuantity { get; init; }

    /// <summary>退货金额合计</summary>
    public required decimal ReturnAmount { get; init; }
}
