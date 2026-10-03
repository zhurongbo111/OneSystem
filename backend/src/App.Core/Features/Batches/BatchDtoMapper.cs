using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Batches;

/// <summary>
/// 批次实体 / 读模型 → 出参模型 的映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class BatchDtoMapper
{
    /// <summary>映射详情出参（新增 / 编辑 / 启停 / 详情共用）</summary>
    public static BatchDetailDto ToDetailDto(Batch batch, string? productCode, string? productName)
        => new()
        {
            Id = batch.Id.ToString(),
            ProductId = batch.ProductId.ToString(),
            ProductCode = productCode ?? string.Empty,
            ProductName = productName ?? string.Empty,
            BatchNo = batch.BatchNo,
            ProductionDate = batch.ProductionDate,
            ExpiryDate = batch.ExpiryDate,
            Status = (int)batch.Status,
            Remark = batch.Remark,
            CreatedAt = batch.CreatedAt,
            UpdatedAt = batch.UpdatedAt,
        };

    /// <summary>映射列表行出参</summary>
    public static BatchListItemDto ToListItemDto(BatchListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            ProductId = item.ProductId.ToString(),
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            BatchNo = item.BatchNo,
            ProductionDate = item.ProductionDate,
            ExpiryDate = item.ExpiryDate,
            Status = (int)item.Status,
            TotalStock = item.TotalStock,
            CreatedAt = item.CreatedAt,
            Remark = item.Remark,
        };

    /// <summary>映射下拉项出参</summary>
    public static BatchPickDto ToPickDto(BatchPickItem item)
        => new()
        {
            BatchId = item.BatchId.ToString(),
            BatchNo = item.BatchNo,
            ProductionDate = item.ProductionDate,
            ExpiryDate = item.ExpiryDate,
            Status = (int)item.Status,
            AvailableQuantity = item.AvailableQuantity,
            IsExpired = item.IsExpired,
            IsNearExpiry = item.IsNearExpiry,
        };
}
