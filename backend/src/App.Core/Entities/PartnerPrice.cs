namespace App.Core.Entities;

/// <summary>
/// 客户协议价实体（对应 PostgreSQL 表 PartnerPrices，specs/036-erp-partner-price/design.md §2.1）。
/// 「客户 × 商品」唯一；只可维护价与备注（客户 / 商品创建后不可改）；删除即回退为商品销售价。
/// 历史单据单价是快照，删除协议价不影响已开单据。
/// </summary>
public sealed class PartnerPrice
{
    /// <summary>协议价 ID</summary>
    public Guid Id { get; set; }

    /// <summary>客户 ID（外键 → Partners(Id)）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>商品 ID（外键 → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>协议单价（取价优先级第 1 位，见 design.md §0.1）</summary>
    public decimal Price { get; set; }

    /// <summary>备注（协议说明 / 生效范围）</summary>
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