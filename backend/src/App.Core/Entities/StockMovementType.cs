namespace App.Core.Entities;

/// <summary>
/// 库存变动类型（对应 StockMovements.MovementType，PG smallint）。
/// 取值 5–10 由后续规格（erp-stock-take / erp-purchase-return / erp-sale-return）追加，
/// 文案与颜色以 specs/019-erp-stock-movement/design.md §0 表为唯一事实源。
/// </summary>
public enum StockMovementType
{
    /// <summary>采购入库（增加）</summary>
    PurchaseInbound = 1,

    /// <summary>采购作废（减少）</summary>
    PurchaseVoid = 2,

    /// <summary>销售出库（减少）</summary>
    SalesOutbound = 3,

    /// <summary>销售作废（增加）</summary>
    SalesVoid = 4,

    /// <summary>期初建账（增加，erp-stock-take）</summary>
    InitialStock = 5,

    /// <summary>盘点调整（增减，erp-stock-take）</summary>
    StockTakeAdjust = 6,

    /// <summary>采购退货（减少，erp-purchase-return）</summary>
    PurchaseReturnOut = 7,

    /// <summary>采购退货作废（增加，erp-purchase-return）</summary>
    PurchaseReturnVoid = 8,
}
