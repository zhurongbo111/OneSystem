namespace App.Core.Entities;

/// <summary>
/// 收付款单类型（specs/023-erp-settlement/design.md §2.1）：
/// 收款单核销销售出库单 / 采购退货单，付款单核销采购入库单 / 销售退货单。
/// </summary>
public enum SettlementType
{
    /// <summary>收款（我们收钱）</summary>
    Receipt = 0,

    /// <summary>付款（我们付钱）</summary>
    Payment = 1,
}
