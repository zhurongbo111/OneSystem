namespace App.Core.Abstractions;

/// <summary>
/// 销售汇总合计模型（对**全量筛选结果**聚合，与当前页无关；净额由 Mapper 在出参层计算）。
/// </summary>
public sealed record SalesSummaryTotal
{
    /// <summary>出库单数合计</summary>
    public required int OrderCount { get; init; }

    /// <summary>出库数量合计</summary>
    public required int OutboundQuantity { get; init; }

    /// <summary>出库金额合计</summary>
    public required decimal OutboundAmount { get; init; }

    /// <summary>退货数量合计</summary>
    public required int ReturnQuantity { get; init; }

    /// <summary>退货金额合计</summary>
    public required decimal ReturnAmount { get; init; }
}
