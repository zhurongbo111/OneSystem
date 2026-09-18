namespace App.Core.Abstractions;

/// <summary>
/// 成本与毛利报表合计读模型（erp-cost）：对**全量筛选结果**聚合，与当前页无关。
/// 毛利与毛利率为派生值，由用例在出参层按 design §0.3 计算（收入为 0 时毛利率为 null）。
/// </summary>
public sealed record CostProfitTotal
{
    /// <summary>销售数量合计</summary>
    public required int SalesQuantity { get; init; }

    /// <summary>销售收入合计</summary>
    public required decimal SalesAmount { get; init; }

    /// <summary>销售成本合计</summary>
    public required decimal CostAmount { get; init; }

    /// <summary>成本缺失标记（存在单价为空或 0 的出库流水）</summary>
    public required bool HasMissingCost { get; init; }
}
