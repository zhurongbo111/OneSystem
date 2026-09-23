namespace App.Core.Entities;

/// <summary>
/// 报价单明细实体（对应 PostgreSQL 表 QuotationItems）。
/// 名称 / 单位 / 单价均为商品当时档案与报价时的**快照**（单价默认按客户协议价带出、可改）；
/// 小计由后端按 数量 × 单价 重算；不软删除，报价单保留明细供审计与转单复制。
/// </summary>
public sealed class QuotationItem
{
    /// <summary>明细行 ID</summary>
    public Guid Id { get; set; }

    /// <summary>报价单 ID（外键 → Quotations(Id)，索引）</summary>
    public Guid QuotationId { get; set; }

    /// <summary>商品 ID（外键 → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>商品名称快照</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>计量单位快照</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>报价数量（≥ 1）</summary>
    public int Quantity { get; set; }

    /// <summary>单价快照（≥ 0，取价规则见 design.md §0.2）</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>小计 = 数量 × 单价（后端重算）</summary>
    public decimal Subtotal { get; set; }
}
