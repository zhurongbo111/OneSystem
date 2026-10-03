using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Inventory;

/// <summary>
/// 库存读模型 → 出参映射（读模型为联查快照，不暴露实体）。
/// 低库存标记（IsBelowSafetyStock）为派生字段，随映射一并计算（SafetyStock &gt; 0 且 Stock &lt; SafetyStock）。
/// 批次视图（040）：到期日 / 过期 / 近效期标记为派生字段，按固定「今天」（UTC 日期）计算。
/// </summary>
internal static class InventoryDtoMapper
{
    /// <summary>列表项读模型 → 库存出参</summary>
    /// <param name="item">读模型</param>
    /// <param name="today">固定「今天」（UTC 日期，用于批次过期 / 近效期判定）</param>
    public static InventoryItemDto ToInventoryItemDto(InventoryItem item, DateOnly today)
        => new()
        {
            ProductId = item.ProductId.ToString(),
            Code = item.Code,
            Name = item.Name,
            CategoryName = item.CategoryName,
            Unit = item.Unit,
            WarehouseId = item.WarehouseId.ToString(),
            WarehouseName = item.WarehouseName,
            BatchId = item.BatchId?.ToString(),
            BatchNo = item.BatchNo,
            ExpiryDate = item.ExpiryDate,
            StockQuantity = item.StockQuantity,
            SafetyStock = item.SafetyStock,
            IsBelowSafetyStock = item.SafetyStock > 0 && item.StockQuantity < item.SafetyStock,
            IsExpired = item.ExpiryDate.HasValue && DateOnly.FromDateTime(item.ExpiryDate.Value.UtcDateTime) < today,
            IsNearExpiry = item.ExpiryDate.HasValue
                && DateOnly.FromDateTime(item.ExpiryDate.Value.UtcDateTime) >= today
                && DateOnly.FromDateTime(item.ExpiryDate.Value.UtcDateTime) <= today.AddDays(BatchFieldConstraints.NearExpiryDays),
            UpdatedAt = item.UpdatedAt,
        };
}
