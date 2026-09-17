namespace App.Core.Entities;

/// <summary>
/// 被核销单据类型（specs/023-erp-settlement/design.md §2.1）。
/// 取值命名沿用「入库 / 出库」语义（与 <c>StockMovementType</c> 同源）：
/// <c>024-erp-order-flow</c> 重命名单据表后取值语义不变。
/// </summary>
public enum SettlementOrderType
{
    /// <summary>采购入库单（付款方向）</summary>
    PurchaseInbound = 0,

    /// <summary>销售出库单（收款方向）</summary>
    SalesOutbound = 1,

    /// <summary>采购退货单（收款方向，供应商退我们钱）</summary>
    PurchaseReturn = 2,

    /// <summary>销售退货单（付款方向，我们退客户钱）</summary>
    SalesReturn = 3,
}
