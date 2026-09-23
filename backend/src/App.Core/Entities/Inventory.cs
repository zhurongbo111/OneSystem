namespace App.Core.Entities;

/// <summary>
/// 库存台账实体（对应 PostgreSQL 表 Inventory），库存的**两个维度**为「商品 × 仓库」
/// （唯一键 <c>(ProductId, WarehouseId)</c>，见 specs/038-erp-multi-warehouse/design.md §0）。
/// 商品新建时为每个启用仓初始化 Quantity = 0；原子增减能力见 IInventoryRepository
/// （IncrementAsync 的 delta 允许为负 —— 采购作废回冲；TryDecrementAsync 条件扣减防超卖）。
/// </summary>
public sealed class Inventory
{
    /// <summary>库存记录 ID</summary>
    public Guid Id { get; set; }

    /// <summary>商品 ID（与 WarehouseId 组成唯一键，外键 → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>仓库 ID（与 ProductId 组成唯一键，外键 → Warehouses(Id)）</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>当前库存（允许为负：仅采购作废回冲可产生，数据异常在库存查询页标红展示）</summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 仓级安全库存阈值（判定唯一来源）：低库存 = <c>SafetyStock &gt; 0 &amp;&amp; Quantity &lt; SafetyStock</c>。
    /// 新建库存行时取商品档案的 <c>Products.SafetyStock</c> 初始值，此后按仓独立维护。
    /// </summary>
    public int SafetyStock { get; set; }

    /// <summary>
    /// 结存成本额（numeric(18,4)，恒 ≥ 0 为正常；可负 = 成本异常，报表标红提示、不阻断业务）。
    /// 口径见 specs/026-erp-cost/design.md §0.1。
    /// </summary>
    public decimal CostAmount { get; set; }

    /// <summary>
    /// 当前移动加权平均单价（numeric(18,4)）：**派生值** = CostAmount / Quantity，
    /// 只在入库成本写入时同步更新；结存为 0 时保留最后均价（供展示与兜底，见 specs/026-erp-cost/design.md §0.1）。
    /// </summary>
    public decimal AverageCost { get; set; }

    /// <summary>最近变动时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
