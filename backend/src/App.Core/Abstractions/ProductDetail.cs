using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 商品详情读模型（联查库存 + 分类名，不暴露实体）
/// </summary>
public sealed record ProductDetail
{
    /// <summary>商品 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>商品编码</summary>
    public required string Code { get; init; }

    /// <summary>商品名称</summary>
    public required string Name { get; init; }

    /// <summary>分类 ID</summary>
    public required Guid CategoryId { get; init; }

    /// <summary>分类名称（联查带出）</summary>
    public required string CategoryName { get; init; }

    /// <summary>计量单位</summary>
    public required string Unit { get; init; }

    /// <summary>默认采购价</summary>
    public required decimal PurchasePrice { get; init; }

    /// <summary>默认销售价</summary>
    public required decimal SalePrice { get; init; }

    /// <summary>安全库存阈值</summary>
    public required int SafetyStock { get; init; }

    /// <summary>当前库存（联查 Inventory 带出）</summary>
    public required int StockQuantity { get; init; }

    /// <summary>商品状态</summary>
    public required ProductStatus Status { get; init; }

    /// <summary>备注</summary>
    public required string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>创建人用户 id</summary>
    public required Guid? CreatedBy { get; init; }

    /// <summary>更新人用户 id</summary>
    public required Guid? UpdatedBy { get; init; }
}
