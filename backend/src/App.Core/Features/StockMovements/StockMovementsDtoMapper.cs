using App.Core.Abstractions;

namespace App.Core.Features.StockMovements;

/// <summary>
/// 库存流水读模型 → 出参映射（读模型为联查快照，不暴露实体）
/// </summary>
internal static class StockMovementsDtoMapper
{
    /// <summary>列表项读模型 → 流水出参（空值原样透传）</summary>
    public static StockMovementListItemDto ToStockMovementListItemDto(StockMovementItem item)
        => new()
        {
            Id = item.Id.ToString(),
            ProductId = item.ProductId.ToString(),
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            Unit = item.Unit,
            MovementType = item.MovementType,
            Quantity = item.Quantity,
            UnitCost = item.UnitCost,
            TotalCost = item.TotalCost,
            SourceNo = item.SourceNo,
            Remark = item.Remark,
            CreatedAt = item.CreatedAt,
            CreatedByName = item.CreatedByName,
        };
}
