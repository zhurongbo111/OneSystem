using App.Core.Entities;

namespace App.Core.Features.Purchases;

/// <summary>
/// 采购单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API）
/// </summary>
internal static class PurchaseDtoMapper
{
    /// <summary>
    /// 主表实体 + 明细行实体转详情 DTO
    /// </summary>
    public static PurchaseOrderDetailDto ToPurchaseOrderDetailDto(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items)
        => new()
        {
            Id = order.Id.ToString(),
            OrderNo = order.OrderNo,
            PartnerId = order.PartnerId.ToString(),
            PartnerName = order.PartnerName,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            SettledAmount = order.SettledAmount,
            UnsettledAmount = SettlementStateCalculator.UnsettledAmount(order.TotalAmount, order.SettledAmount),
            SettlementState = (int)SettlementStateCalculator.Derive(order.TotalAmount, order.SettledAmount),
            Status = (int)order.Status,
            Remark = order.Remark,
            CreatedBy = order.CreatedBy?.ToString(),
            CreatedAt = order.CreatedAt,
            Items = items.Select(i => new PurchaseOrderItemDto
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

    /// <summary>
    /// 主表实体转列表 DTO
    /// </summary>
    public static PurchaseOrderListItemDto ToPurchaseOrderListItemDto(PurchaseOrder order)
        => new()
        {
            Id = order.Id.ToString(),
            OrderNo = order.OrderNo,
            PartnerId = order.PartnerId.ToString(),
            PartnerName = order.PartnerName,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            SettledAmount = order.SettledAmount,
            UnsettledAmount = SettlementStateCalculator.UnsettledAmount(order.TotalAmount, order.SettledAmount),
            SettlementState = (int)SettlementStateCalculator.Derive(order.TotalAmount, order.SettledAmount),
            Status = (int)order.Status,
            CreatedAt = order.CreatedAt,
        };
}
