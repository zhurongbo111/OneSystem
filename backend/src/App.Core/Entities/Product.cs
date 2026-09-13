namespace App.Core.Entities;

/// <summary>
/// 商品实体（对应 PostgreSQL 表 Products）。
/// 简单 SKU 模型：编码 / 名称 / 单级分类 / 单位 / 采购价 / 销售价 / 安全库存阈值。
/// 商品编码创建后不可修改；停用不删除，保留历史单据引用；当前库存见 <see cref="Inventory"/>。
/// </summary>
public sealed class Product
{
    /// <summary>商品 ID</summary>
    public Guid Id { get; set; }

    /// <summary>商品编码，唯一，创建后不可修改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>商品名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>商品分类 ID（单级，外键 → Categories(Id)）</summary>
    public Guid CategoryId { get; set; }

    /// <summary>计量单位（个 / 箱 / 斤…）</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>默认采购价</summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>默认销售价</summary>
    public decimal SalePrice { get; set; }

    /// <summary>安全库存阈值（低库存提醒；0 表示不提醒）</summary>
    public int SafetyStock { get; set; }

    /// <summary>商品状态（启用 / 停用；停用不可被新单据选择）</summary>
    public ProductStatus Status { get; set; } = ProductStatus.Enabled;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
