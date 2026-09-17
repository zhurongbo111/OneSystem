using App.Core.Entities;

namespace App.Core.Features.PurchaseReceipts;

/// <summary>
/// 采购入库单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API）
/// </summary>
internal static class PurchaseReceiptsDtoMapper
{
    /// <summary>
    /// 主表实体 + 明细行实体转详情 DTO
    /// </summary>
    public static PurchaseReceiptDetailDto ToPurchaseReceiptDetailDto(PurchaseReceipt order, IReadOnlyList<PurchaseReceiptItem> items)
        => new()
        {
            Id = order.Id.ToString(),
            ReceiptNo = order.ReceiptNo,
            PartnerId = order.PartnerId.ToString(),
            PartnerName = order.PartnerName,
            OrderDate = order.OrderDate,
            OrderId = order.OrderId?.ToString(),
            OrderNo = order.OrderNo,
            TotalAmount = order.TotalAmount,
            SettledAmount = order.SettledAmount,
            UnsettledAmount = SettlementStateCalculator.UnsettledAmount(order.TotalAmount, order.SettledAmount),
            SettlementState = (int)SettlementStateCalculator.Derive(order.TotalAmount, order.SettledAmount),
            Status = (int)order.Status,
            Remark = order.Remark,
            CreatedBy = order.CreatedBy?.ToString(),
            CreatedAt = order.CreatedAt,
            Items = items.Select(i => new PurchaseReceiptItemDto
            {
                Id = i.Id.ToString(),
                ProductId = i.ProductId.ToString(),
                ProductName = i.ProductName,
                Unit = i.Unit,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal,
                OrderItemId = i.OrderItemId?.ToString(),
            }).ToList(),
        };

    /// <summary>
    /// 主表实体转列表 DTO
    /// </summary>
    public static PurchaseReceiptListItemDto ToPurchaseReceiptListItemDto(PurchaseReceipt order)
        => new()
        {
            Id = order.Id.ToString(),
            ReceiptNo = order.ReceiptNo,
            PartnerId = order.PartnerId.ToString(),
            PartnerName = order.PartnerName,
            OrderDate = order.OrderDate,
            OrderId = order.OrderId?.ToString(),
            OrderNo = order.OrderNo,
            TotalAmount = order.TotalAmount,
            SettledAmount = order.SettledAmount,
            UnsettledAmount = SettlementStateCalculator.UnsettledAmount(order.TotalAmount, order.SettledAmount),
            SettlementState = (int)SettlementStateCalculator.Derive(order.TotalAmount, order.SettledAmount),
            Status = (int)order.Status,
            CreatedAt = order.CreatedAt,
        };

    /// <summary>
    /// 订单实体转「可关联订单候选」DTO
    /// </summary>
    public static PurchaseOrderPickDto ToPurchaseOrderPickDto(PurchaseOrder order)
        => new()
        {
            Id = order.Id.ToString(),
            OrderNo = order.OrderNo,
            OrderDate = order.OrderDate,
            ExpectedDate = order.ExpectedDate,
            TotalAmount = order.TotalAmount,
        };

    /// <summary>
    /// 订单实体 + 明细转「关联订单明细」DTO（未收数量 = Quantity − FulfilledQuantity）
    /// </summary>
    public static PurchaseOrderLinesDto ToPurchaseOrderLinesDto(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items)
        => new()
        {
            OrderId = order.Id.ToString(),
            OrderNo = order.OrderNo,
            PartnerId = order.PartnerId.ToString(),
            PartnerName = order.PartnerName,
            ExpectedDate = order.ExpectedDate,
            Items = items.Select(i => new PurchaseOrderLineDto
            {
                OrderItemId = i.Id.ToString(),
                ProductId = i.ProductId.ToString(),
                ProductName = i.ProductName,
                Unit = i.Unit,
                Quantity = i.Quantity,
                FulfilledQuantity = i.FulfilledQuantity,
                RemainingQuantity = i.Quantity - i.FulfilledQuantity,
                UnitPrice = i.UnitPrice,
            }).ToList(),
        };
}
