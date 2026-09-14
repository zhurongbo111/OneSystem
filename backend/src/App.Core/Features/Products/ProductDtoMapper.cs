using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Features.Products.GetProductPickList;

namespace App.Core.Features.Products;

/// <summary>
/// 商品出参映射（集中一处，避免各用例重复拼装）。
/// 低库存标记（IsBelowSafetyStock）为派生字段，随映射一并计算（SafetyStock &gt; 0 且 Stock &lt; SafetyStock）。
/// 重载按源类型区分：读模型（ProductListItem / ProductDetail）与实体（Product + 上下文）各一。
/// </summary>
internal static class ProductDtoMapper
{
    /// <summary>列表读模型 → 商品出参（列表读模型不含备注，Remark 置 null）</summary>
    public static ProductDto ToProductDto(ProductListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Code = item.Code,
            Name = item.Name,
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            Unit = item.Unit,
            PurchasePrice = item.PurchasePrice,
            SalePrice = item.SalePrice,
            SafetyStock = item.SafetyStock,
            StockQuantity = item.StockQuantity,
            IsBelowSafetyStock = IsBelowSafetyStock(item.SafetyStock, item.StockQuantity),
            Status = (int)item.Status,
            Remark = null,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };

    /// <summary>详情读模型 → 商品出参（联查带出分类名 / 备注）</summary>
    public static ProductDto ToProductDto(ProductDetail detail)
        => new()
        {
            Id = detail.Id.ToString(),
            Code = detail.Code,
            Name = detail.Name,
            CategoryId = detail.CategoryId.ToString(),
            CategoryName = detail.CategoryName,
            Unit = detail.Unit,
            PurchasePrice = detail.PurchasePrice,
            SalePrice = detail.SalePrice,
            SafetyStock = detail.SafetyStock,
            StockQuantity = detail.StockQuantity,
            IsBelowSafetyStock = IsBelowSafetyStock(detail.SafetyStock, detail.StockQuantity),
            Status = (int)detail.Status,
            Remark = detail.Remark,
            CreatedAt = detail.CreatedAt,
            UpdatedAt = detail.UpdatedAt,
        };

    /// <summary>
    /// 实体 → 商品出参（写操作用例：分类名 / 当前库存由用例查库后传入）。
    /// </summary>
    public static ProductDto ToProductDto(Product product, string categoryName, int stockQuantity)
        => new()
        {
            Id = product.Id.ToString(),
            Code = product.Code,
            Name = product.Name,
            CategoryId = product.CategoryId.ToString(),
            CategoryName = categoryName,
            Unit = product.Unit,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice,
            SafetyStock = product.SafetyStock,
            StockQuantity = stockQuantity,
            IsBelowSafetyStock = IsBelowSafetyStock(product.SafetyStock, stockQuantity),
            Status = (int)product.Status,
            Remark = product.Remark,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
        };

    /// <summary>开单选品读模型 → 开单选品出参</summary>
    public static ProductPickDto ToProductPickDto(ProductPickItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Code = item.Code,
            Name = item.Name,
            Unit = item.Unit,
            PurchasePrice = item.PurchasePrice,
            SalePrice = item.SalePrice,
            StockQuantity = item.StockQuantity,
        };

    /// <summary>低库存判定：阈值为 0 不提醒，否则当前库存小于阈值即提醒</summary>
    private static bool IsBelowSafetyStock(int safetyStock, int stockQuantity)
        => safetyStock > 0 && stockQuantity < safetyStock;
}
