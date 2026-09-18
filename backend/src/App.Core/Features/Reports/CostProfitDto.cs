namespace App.Core.Features.Reports;

/// <summary>
/// 成本与毛利报表行出参（erp-cost）：毛利 = 销售收入 − 销售成本；毛利率在收入为 0 时为 <c>null</c>（前端显示 `-`）。
/// 金额为 4 位存储口径，前端展示时收敛到 2 位（design §0.1）。
/// </summary>
public sealed class CostProfitItemDto
{
    /// <summary>分组键（单据 id / 商品 id / 往来单位 id）</summary>
    public string? Key { get; init; }

    /// <summary>分组名称（单号 / 商品名 / 客户名）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>销售数量（出库 − 退货）</summary>
    public int SalesQuantity { get; init; }

    /// <summary>销售收入</summary>
    public decimal SalesAmount { get; init; }

    /// <summary>销售成本</summary>
    public decimal CostAmount { get; init; }

    /// <summary>毛利 = 销售收入 − 销售成本</summary>
    public decimal GrossProfit { get; init; }

    /// <summary>毛利率（销售收入为 0 时为 null）</summary>
    public decimal? GrossProfitRate { get; init; }

    /// <summary>成本不完整（期间内存在单价为空或 0 的出库流水）</summary>
    public bool HasMissingCost { get; init; }
}

/// <summary>
/// 成本与毛利报表合计出参（全量筛选结果口径）。
/// </summary>
public sealed class CostProfitTotalDto
{
    /// <summary>销售数量合计</summary>
    public int SalesQuantity { get; init; }

    /// <summary>销售收入合计</summary>
    public decimal SalesAmount { get; init; }

    /// <summary>销售成本合计</summary>
    public decimal CostAmount { get; init; }

    /// <summary>毛利合计</summary>
    public decimal GrossProfit { get; init; }

    /// <summary>毛利率合计（收入为 0 时为 null）</summary>
    public decimal? GrossProfitRate { get; init; }

    /// <summary>是否存在成本不完整</summary>
    public bool HasMissingCost { get; init; }
}
