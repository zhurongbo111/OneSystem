using App.Core.Abstractions;

namespace App.Core.Features.Inventory;

/// <summary>
/// 库存读模型 → 出参映射（读模型为联查快照，不暴露实体）。
/// 低库存标记（IsBelowSafetyStock）为派生字段，随映射一并计算（SafetyStock &gt; 0 且 Stock &lt; SafetyStock）。
/// </summary>
internal static class InventoryDtoMapper
{
    /// <summary>列表项读模型 → 库存出参</summary>
    public static InventoryItemDto ToInventoryItemDto(InventoryItem item)
        => new()
        {
            ProductId = item.ProductId.ToString(),
            Code = item.Code,
            Name = item.Name,
            CategoryName = item.CategoryName,
            Unit = item.Unit,
            WarehouseId = item.WarehouseId.ToString(),
            WarehouseName = item.WarehouseName,
            StockQuantity = item.StockQuantity,
            SafetyStock = item.SafetyStock,
            IsBelowSafetyStock = item.SafetyStock > 0 && item.StockQuantity < item.SafetyStock,
            UpdatedAt = item.UpdatedAt,
        };
}
