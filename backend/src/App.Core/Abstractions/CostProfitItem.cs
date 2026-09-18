namespace App.Core.Abstractions;

/// <summary>
/// 成本与毛利报表行读模型（按单据 / 商品 / 往来单位维度聚合）。
/// 销售收入取自未作废销售出库单与销售退货单，销售成本取自对应流水的 <c>TotalCost</c>（天然冲减）；
/// 毛利与毛利率为派生值，由用例在出参层按 specs/026-erp-cost/design.md §0.3 计算（收入为 0 时毛利率为 null）。
/// </summary>
public sealed record CostProfitItem
{
    /// <summary>分组键（单据 id / 商品 id / 往来单位 id；商品与往来维度恒有值）</summary>
    public Guid? Key { get; init; }

    /// <summary>分组名称（单号 / 商品名 / 往来单位名）</summary>
    public required string Name { get; init; }

    /// <summary>销售数量（出库数量 − 退货数量）</summary>
    public required int SalesQuantity { get; init; }

    /// <summary>销售收入（出库金额 − 退货金额）</summary>
    public required decimal SalesAmount { get; init; }

    /// <summary>销售成本（出库流水成本 + 退货入库流水成本，退货为正故天然冲减）</summary>
    public required decimal CostAmount { get; init; }

    /// <summary>毛利 = 销售收入 − 销售成本</summary>
    public decimal GrossProfit { get; init; }

    /// <summary>毛利率（销售收入为 0 时为 null）</summary>
    public decimal? GrossProfitRate { get; init; }

    /// <summary>是否存在成本缺失（期间内存在单价为空或 0 的出库流水）</summary>
    public required bool HasMissingCost { get; init; }
}
