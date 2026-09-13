namespace App.Core.Features.Products;

/// <summary>
/// 商品出参模型（列表 / 详情 / 新增 / 编辑共用）。
/// 枚举统一以**整型**输出（ProductStatus：0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class ProductDto
{
    /// <summary>商品 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>商品编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>商品名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>分类 ID</summary>
    public string CategoryId { get; init; } = string.Empty;

    /// <summary>分类名称</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>计量单位</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>默认采购价</summary>
    public decimal PurchasePrice { get; init; }

    /// <summary>默认销售价</summary>
    public decimal SalePrice { get; init; }

    /// <summary>安全库存阈值</summary>
    public int SafetyStock { get; init; }

    /// <summary>当前库存</summary>
    public int StockQuantity { get; init; }

    /// <summary>是否低库存（当前库存 &lt; 安全库存阈值，且阈值 &gt; 0；由 Handler 计算）</summary>
    public bool IsBelowSafetyStock { get; init; }

    /// <summary>商品状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
