namespace App.Core.Features.Costs;

/// <summary>
/// 成本重算出参（erp-cost design §3.3）：本次重算的流水条数、缺价条数、涉及商品数。
/// 缺价 &gt; 0 表示存在「单价无法推算」的流水（如本规格上线前建立的期初），需人工补录成本或执行盘点调整。
/// </summary>
public sealed class CostRecalculateResultDto
{
    /// <summary>重算（写回）的流水条数</summary>
    public int MovementCount { get; init; }

    /// <summary>单价无法推算、按 0 计入的流水条数</summary>
    public int MissingCostCount { get; init; }

    /// <summary>涉及商品数</summary>
    public int ProductCount { get; init; }
}
