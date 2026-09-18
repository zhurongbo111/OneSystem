namespace App.Core.Entities;

/// <summary>
/// 库存变动流水实体（对应 PostgreSQL 表 StockMovements）。
/// 纯追加表：只写入、不更新、不删除，作为库存变动审计轨迹；
/// Quantity 带符号（入库 / 回增为正，出库 / 回冲为负），不落变动前后库存。
/// 商品编码 / 名称 / 单位与操作人姓名由查询侧联查带出，本表不存快照。
/// </summary>
public sealed class StockMovement
{
    /// <summary>流水 ID</summary>
    public Guid Id { get; set; }

    /// <summary>变动商品 id（FK → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>变动类型</summary>
    public StockMovementType MovementType { get; set; }

    /// <summary>变动量（带符号：入库 / 回增为正，出库 / 回冲为负；恒不为 0）</summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 本次变动成本单价（numeric(18,4)）：按 specs/026-erp-cost/design.md §0.2 的来源表取值
    ///（入库取单据价 / 录入价，出库取变动前均价，冲销类复用原方向单价）。
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>本次变动成本金额（numeric(18,4)，与 <see cref="Quantity"/> 同号；恒等式 Σ TotalCost == Inventory.CostAmount）</summary>
    public decimal TotalCost { get; set; }

    /// <summary>来源单据 id（采购单 / 销售单）</summary>
    public Guid? SourceId { get; set; }

    /// <summary>来源单据号（单号是不可变标识，存值使列表免 join；无来源单据时为 null）</summary>
    public string? SourceNo { get; set; }

    /// <summary>备注（本期无写入来源，预留展示位）</summary>
    public string? Remark { get; set; }

    /// <summary>变动时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>操作人用户 id（系统操作可空）</summary>
    public Guid? CreatedBy { get; set; }
}
