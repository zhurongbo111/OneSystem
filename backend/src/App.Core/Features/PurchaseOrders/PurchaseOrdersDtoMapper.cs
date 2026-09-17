using App.Core.Entities;

namespace App.Core.Features.PurchaseOrders;

/// <summary>
/// 采购订单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API）；
/// 未执行量（Quantity − FulfilledQuantity）等派生字段在 Mapper 内计算。
/// </summary>
internal static class PurchaseOrdersDtoMapper
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
            ExpectedDate = order.ExpectedDate,
            TotalAmount = order.TotalAmount,
            FlowStatus = (int)order.FlowStatus,
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
                FulfilledQuantity = i.FulfilledQuantity,
                RemainingQuantity = i.Quantity - i.FulfilledQuantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal,
            }).ToList(),
        };

    /// <summary>
    /// 主表实体 + 未收数量合计转列表 DTO
    /// </summary>
    public static PurchaseOrderListItemDto ToPurchaseOrderListItemDto(PurchaseOrder order, int unfulfilledQuantity)
        => new()
        {
            Id = order.Id.ToString(),
            OrderNo = order.OrderNo,
            PartnerId = order.PartnerId.ToString(),
            PartnerName = order.PartnerName,
            OrderDate = order.OrderDate,
            ExpectedDate = order.ExpectedDate,
            TotalAmount = order.TotalAmount,
            UnfulfilledQuantity = unfulfilledQuantity,
            FlowStatus = (int)order.FlowStatus,
            CreatedAt = order.CreatedAt,
        };
}
