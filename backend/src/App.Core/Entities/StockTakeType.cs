namespace App.Core.Entities;

/// <summary>
/// 盘点单据类型（对应 StockTakes.Type，PG smallint）。
/// 期初建账与库存盘点共用一套单据结构与提交流程，仅流水类型与可选商品范围不同（见 design.md §5）。
/// </summary>
public enum StockTakeType
{
    /// <summary>期初建账（仅允许从未发生库存变动的商品）</summary>
    Initial = 0,

    /// <summary>库存盘点（任意商品，按实盘数量校正账面）</summary>
    Take = 1,
}
