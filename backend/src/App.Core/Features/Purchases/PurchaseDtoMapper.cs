using App.Core.Abstractions;

namespace App.Core.Features.Purchases;

/// <summary>
/// 采购单读模型 → 出参映射（读模型为实体快照，映射为纯赋值，禁止反向依赖实体暴露）
/// </summary>
internal static class PurchaseDtoMapper
{
    /// <summary>
    /// 详情读模型转 DTO
    /// </summary>
    public static PurchaseOrderDetailDto ToDetailDto(PurchaseOrderDetail detail)
        => new()
        {
            Id = detail.Id.ToString(),
            OrderNo = detail.OrderNo,
            PartnerId = detail.PartnerId.ToString(),
            PartnerName = detail.PartnerName,
            OrderDate = detail.OrderDate,
            TotalAmount = detail.TotalAmount,
            SettlementStatus = (int)detail.SettlementStatus,
            Status = (int)detail.Status,
            Remark = detail.Remark,
            CreatedBy = detail.CreatedBy?.ToString(),
            CreatedAt = detail.CreatedAt,
            Items = detail.Items.Select(i => new PurchaseOrderItemDto
            {
                Id = i.Id.ToString(),
                ProductId = i.ProductId.ToString(),
                ProductName = i.ProductName,
                Unit = i.Unit,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal,
            }).ToList(),
        };
}
