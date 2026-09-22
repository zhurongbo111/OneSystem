namespace App.Core.Entities;

/// <summary>
/// 凭证来源类型（specs/033-erp-general-ledger/design.md §0.1）：
/// 手工凭证与 6 类单据自动凭证，外加销售成本结转
/// </summary>
public enum VoucherSourceType
{
    /// <summary>手工凭证</summary>
    Manual = 0,

    /// <summary>采购入库单</summary>
    PurchaseInbound = 1,

    /// <summary>销售出库单</summary>
    SalesOutbound = 2,

    /// <summary>采购退货单</summary>
    PurchaseReturn = 3,

    /// <summary>销售退货单</summary>
    SalesReturn = 4,

    /// <summary>收款单</summary>
    Receipt = 5,

    /// <summary>付款单</summary>
    Payment = 6,

    /// <summary>销售成本结转</summary>
    CostCarry = 7,
}
