namespace App.Core.Features.Reports;

/// <summary>
/// 汇总报表的分组维度（采购 / 销售汇总共用；仅请求入参使用，不落库）。
/// 口径见 specs/025-erp-report/design.md §3.4。
/// </summary>
public enum SummaryGroupBy
{
    /// <summary>按往来单位分组（默认：采购按供应商、销售按客户）</summary>
    Partner = 0,

    /// <summary>按商品分组（往来列不展示，改展示商品名称与单位）</summary>
    Product = 1,
}
