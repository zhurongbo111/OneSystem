namespace App.Core.Entities;

/// <summary>
/// 发票类型（specs/032-erp-invoice/design.md §2.3）：
/// 进项 = 采购取得（关联采购类单据）、销项 = 销售开出（关联销售类单据）
/// </summary>
public enum InvoiceType
{
    /// <summary>进项（采购取得）</summary>
    Purchase = 0,

    /// <summary>销项（销售开出）</summary>
    Sales = 1,
}