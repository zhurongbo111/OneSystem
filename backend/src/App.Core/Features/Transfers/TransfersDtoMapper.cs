using App.Core.Entities;

namespace App.Core.Features.Transfers;

/// <summary>
/// 调拨单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API）。
/// 调拨无金额 / 无结算状态推导，映射比采购退货更简单；明细不含 BatchId（040 前恒为空）。
/// </summary>
internal static class TransfersDtoMapper
{
    /// <summary>
    /// 主表实体 + 明细行实体转详情 DTO
    /// </summary>
    public static TransferDetailDto ToTransferDetailDto(Transfer transfer, IReadOnlyList<TransferItem> items)
        => new()
        {
            Id = transfer.Id.ToString(),
            TransferNo = transfer.TransferNo,
            FromWarehouseId = transfer.FromWarehouseId.ToString(),
            FromWarehouseName = transfer.FromWarehouseName,
            ToWarehouseId = transfer.ToWarehouseId.ToString(),
            ToWarehouseName = transfer.ToWarehouseName,
            TransferDate = transfer.TransferDate,
            ItemCount = transfer.ItemCount,
            TotalQuantity = transfer.TotalQuantity,
            Status = (int)transfer.Status,
            Remark = transfer.Remark,
            CreatedBy = transfer.CreatedBy?.ToString(),
            CreatedAt = transfer.CreatedAt,
            Items = items.Select(i => new TransferItemDto
            {
                Id = i.Id.ToString(),
                ProductId = i.ProductId.ToString(),
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                Unit = i.Unit,
                Quantity = i.Quantity,
            }).ToList(),
        };

    /// <summary>
    /// 主表实体转列表 DTO
    /// </summary>
    public static TransferListItemDto ToTransferListItemDto(Transfer transfer)
        => new()
        {
            Id = transfer.Id.ToString(),
            TransferNo = transfer.TransferNo,
            FromWarehouseId = transfer.FromWarehouseId.ToString(),
            FromWarehouseName = transfer.FromWarehouseName,
            ToWarehouseId = transfer.ToWarehouseId.ToString(),
            ToWarehouseName = transfer.ToWarehouseName,
            TransferDate = transfer.TransferDate,
            ItemCount = transfer.ItemCount,
            TotalQuantity = transfer.TotalQuantity,
            Status = (int)transfer.Status,
            CreatedAt = transfer.CreatedAt,
        };
}
