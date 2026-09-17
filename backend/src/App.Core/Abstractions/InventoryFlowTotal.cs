namespace App.Core.Abstractions;

/// <summary>
/// 进销存报表合计模型（对**全量筛选结果**聚合，与当前页无关；specs/025-erp-report design.md §5）。
/// </summary>
public sealed record InventoryFlowTotal
{
    /// <summary>期初数量合计</summary>
    public required int OpeningQuantity { get; init; }

    /// <summary>期间入合计</summary>
    public required int InboundQuantity { get; init; }

    /// <summary>期间出合计</summary>
    public required int OutboundQuantity { get; init; }

    /// <summary>期末数量合计</summary>
    public required int ClosingQuantity { get; init; }
}
