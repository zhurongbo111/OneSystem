namespace App.Core.Entities;

/// <summary>
/// 销售订单明细实体（对应 PostgreSQL 表 SalesOrderItems，与 <c>PurchaseOrderItem</c> 同构）。
/// 名称 / 单位 / 单价均为商品当时档案的**快照**（单价默认带出销售价、开单时可改）；
/// 小计由后端按 数量 × 单价 重算；不软删除，订单保留明细供审计。
/// </summary>
public sealed class SalesOrderItem
{
    /// <summary>明细行 ID</summary>
    public Guid Id { get; set; }

    /// <summary>订单 ID（外键 → SalesOrders(Id)，索引）</summary>
    public Guid OrderId { get; set; }

    /// <summary>商品 ID（外键 → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>商品名称快照</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>计量单位快照</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>订购数量（≥ 1）</summary>
    public int Quantity { get; set; }

    /// <summary>单价快照（≥ 0）</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>小计 = 数量 × 单价（后端重算）</summary>
    public decimal Subtotal { get; set; }

    /// <summary>累计已发数量（由销售出库单关联回写 / 作废回退；未执行量 = Quantity − FulfilledQuantity 由后端推导，不落列）</summary>
    public int FulfilledQuantity { get; set; }
}
