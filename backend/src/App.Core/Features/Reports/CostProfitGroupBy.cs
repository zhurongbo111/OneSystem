namespace App.Core.Features.Reports;

/// <summary>
/// 成本与毛利报表的分组维度（erp-cost；仅请求入参使用，不落库）。
/// 与 <see cref="SummaryGroupBy"/>（采购 / 销售汇总）分列：本枚举多一个「单据」维度，用于按单看毛利。
/// </summary>
public enum CostProfitGroupBy
{
    /// <summary>按销售单据分组（默认；出库单与退货单各自成行）</summary>
    Order = 0,

    /// <summary>按商品分组</summary>
    Product = 1,

    /// <summary>按往来单位（客户）分组</summary>
    Partner = 2,
}
