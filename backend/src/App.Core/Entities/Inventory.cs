namespace App.Core.Entities;

/// <summary>
/// 库存台账实体（对应 PostgreSQL 表 Inventory），与商品 1:1（ProductId 唯一，单仓库）。
/// 商品新建时初始化 Quantity = 0；原子增减能力见 IInventoryRepository
/// （IncrementAsync 的 delta 允许为负 —— 采购作废回冲；TryDecrementAsync 条件扣减防超卖）。
/// </summary>
public sealed class Inventory
{
    /// <summary>库存记录 ID</summary>
    public Guid Id { get; set; }

    /// <summary>商品 ID（唯一，外键 → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>当前库存（允许为负：仅采购作废回冲可产生，数据异常在库存查询页标红展示）</summary>
    public int Quantity { get; set; }

    /// <summary>最近变动时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
