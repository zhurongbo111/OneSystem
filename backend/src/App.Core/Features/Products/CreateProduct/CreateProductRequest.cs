using App.Core.Abstractions;

namespace App.Core.Features.Products.CreateProduct;

/// <summary>
/// 新增商品请求：创建时同步初始化库存行（Quantity = 0），同一事务内完成
/// </summary>
public sealed class CreateProductRequest : IRequest<ProductDto>
{
    /// <summary>商品编码（2–32 位字母 / 数字 / 下划线 / 连字符，唯一，创建后不可修改）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>商品名称（2–50 字符）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>商品分类 id（必选）</summary>
    public Guid CategoryId { get; init; }

    /// <summary>计量单位（1–10 字符）</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>默认采购价（0–9999999.99）</summary>
    public decimal PurchasePrice { get; init; }

    /// <summary>默认销售价（0–9999999.99）</summary>
    public decimal SalePrice { get; init; }

    /// <summary>安全库存阈值（0–999999；0 表示不提醒）</summary>
    public int SafetyStock { get; init; }

    /// <summary>备注，可空（≤ 200 字符）</summary>
    public string? Remark { get; init; }
}
