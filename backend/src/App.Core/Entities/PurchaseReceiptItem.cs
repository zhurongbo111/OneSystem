namespace App.Core.Entities;

/// <summary>
/// 采购单明细实体（对应 PostgreSQL 表 PurchaseReceiptItems，销售单 <c>SalesShipmentItem</c> 同构）。
/// 名称 / 单位 / 单价均为商品当时档案的**快照**（单价默认带出采购价、开单时可改）；
/// 小计由后端按 数量 × 单价 重算；不软删除，作废单保留明细供审计。
/// </summary>
public sealed class PurchaseReceiptItem
{
    /// <summary>明细行 ID</summary>
    public Guid Id { get; set; }

    /// <summary>采购单 ID（外键 → PurchaseReceipts(Id)，索引）</summary>
    public Guid ReceiptId { get; set; }

    /// <summary>商品 ID（外键 → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>商品名称快照</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>计量单位快照</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>数量（≥ 1）</summary>
    public int Quantity { get; set; }

    /// <summary>单价快照（≥ 0）</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>小计 = 数量 × 单价（后端重算）</summary>
    public decimal Subtotal { get; set; }

    /// <summary>关联采购订单明细行 id（可空：关联订单时必填，用于回写订单明细的累计已收数量）</summary>
    public Guid? OrderItemId { get; set; }
}
