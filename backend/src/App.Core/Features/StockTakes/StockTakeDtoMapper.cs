using App.Core.Entities;

namespace App.Core.Features.StockTakes;

/// <summary>
/// 盘点 / 期初建账单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API）。
/// </summary>
internal static class StockTakeDtoMapper
{
    /// <summary>
    /// 主表实体 + 明细行实体转详情 DTO
    /// </summary>
    public static StockTakeDetailDto ToStockTakeDetailDto(StockTake take, IReadOnlyList<StockTakeItem> items)
        => new()
        {
            Id = take.Id.ToString(),
            TakeNo = take.TakeNo,
            Type = (int)take.Type,
            WarehouseId = take.WarehouseId.ToString(),
            WarehouseName = take.WarehouseName,
            TakeDate = take.TakeDate,
            ItemCount = take.ItemCount,
            DiffItemCount = take.DiffItemCount,
            Remark = take.Remark,
            CreatedBy = take.CreatedBy?.ToString(),
            CreatedAt = take.CreatedAt,
            Items = items.Select(i => new StockTakeItemDto
            {
                Id = i.Id.ToString(),
                ProductId = i.ProductId.ToString(),
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                Unit = i.Unit,
                BookQuantity = i.BookQuantity,
                ActualQuantity = i.ActualQuantity,
                Difference = i.Difference,
                UnitCost = i.UnitCost,
            }).ToList(),
        };

    /// <summary>
    /// 主表实体转列表 DTO
    /// </summary>
    public static StockTakeListItemDto ToStockTakeListItemDto(StockTake take)
        => new()
        {
            Id = take.Id.ToString(),
            TakeNo = take.TakeNo,
            Type = (int)take.Type,
            WarehouseId = take.WarehouseId.ToString(),
            WarehouseName = take.WarehouseName,
            TakeDate = take.TakeDate,
            ItemCount = take.ItemCount,
            DiffItemCount = take.DiffItemCount,
            Remark = take.Remark,
            CreatedAt = take.CreatedAt,
        };
}
