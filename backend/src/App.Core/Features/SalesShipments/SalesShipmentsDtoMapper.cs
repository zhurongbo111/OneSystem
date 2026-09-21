using App.Core.Entities;

namespace App.Core.Features.SalesShipments;

/// <summary>
/// 销售出库单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API）
/// </summary>
internal static class SalesShipmentsDtoMapper
{
    /// <summary>
    /// 主表实体 + 明细行实体转详情 DTO
    /// </summary>
    public static SalesShipmentDetailDto ToSalesShipmentDetailDto(SalesShipment order, IReadOnlyList<SalesShipmentItem> items)
        => new()
        {
            Id = order.Id.ToString(),
            ShipmentNo = order.ShipmentNo,
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
            TotalQuantity = items.Sum(i => i.Quantity),
            Items = items.Select(i => new SalesShipmentItemDto
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
    /// 主表实体 + 数量合计转列表 DTO（数量合计由仓储按明细聚合）
    /// </summary>
    /// <param name="order">出库单实体</param>
    /// <param name="totalQuantity">数量合计（= Σ 明细数量）</param>
    public static SalesShipmentListItemDto ToSalesShipmentListItemDto(SalesShipment order, int totalQuantity)
        => new()
        {
            Id = order.Id.ToString(),
            ShipmentNo = order.ShipmentNo,
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
            TotalQuantity = totalQuantity,
        };

    /// <summary>
    /// 订单实体转「可关联订单候选」DTO
    /// </summary>
    public static SalesOrderPickDto ToSalesOrderPickDto(SalesOrder order)
        => new()
        {
            Id = order.Id.ToString(),
            OrderNo = order.OrderNo,
            OrderDate = order.OrderDate,
            ExpectedDate = order.ExpectedDate,
            TotalAmount = order.TotalAmount,
        };

    /// <summary>
    /// 订单实体 + 明细转「关联订单明细」DTO（未发数量 = Quantity − FulfilledQuantity）
    /// </summary>
    public static SalesOrderLinesDto ToSalesOrderLinesDto(SalesOrder order, IReadOnlyList<SalesOrderItem> items)
        => new()
        {
            OrderId = order.Id.ToString(),
            OrderNo = order.OrderNo,
            PartnerId = order.PartnerId.ToString(),
            PartnerName = order.PartnerName,
            ExpectedDate = order.ExpectedDate,
            Items = items.Select(i => new SalesOrderLineDto
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
