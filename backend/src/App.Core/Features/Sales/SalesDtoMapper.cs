using App.Core.Abstractions;

namespace App.Core.Features.Sales;

/// <summary>
/// 销售单读模型 → 出参映射（读模型为实体快照，映射为纯赋值，禁止反向依赖实体暴露）
/// </summary>
internal static class SalesDtoMapper
{
    /// <summary>
    /// 详情读模型转 DTO
    /// </summary>
    public static SalesOrderDetailDto ToSalesOrderDetailDto(SalesOrderDetail detail)
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
            Items = detail.Items.Select(i => new SalesOrderItemDto
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
    /// 列表读模型转 DTO
    /// </summary>
    public static SalesOrderListItemDto ToSalesOrderListItemDto(SalesOrderListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            OrderNo = item.OrderNo,
            PartnerId = item.PartnerId.ToString(),
            PartnerName = item.PartnerName,
            OrderDate = item.OrderDate,
            TotalAmount = item.TotalAmount,
            SettlementStatus = (int)item.SettlementStatus,
            Status = (int)item.Status,
            CreatedAt = item.CreatedAt,
        };
}
