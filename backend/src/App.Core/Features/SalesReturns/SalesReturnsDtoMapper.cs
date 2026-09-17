using App.Core.Entities;

namespace App.Core.Features.SalesReturns;

/// <summary>
/// 销售退货单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API）
/// </summary>
internal static class SalesReturnsDtoMapper
{
    /// <summary>
    /// 主表实体 + 明细行实体转详情 DTO
    /// </summary>
    public static SalesReturnDetailDto ToSalesReturnDetailDto(SalesReturn salesReturn, IReadOnlyList<SalesReturnItem> items)
        => new()
        {
            Id = salesReturn.Id.ToString(),
            ReturnNo = salesReturn.ReturnNo,
            PartnerId = salesReturn.PartnerId.ToString(),
            PartnerName = salesReturn.PartnerName,
            ReturnDate = salesReturn.ReturnDate,
            TotalAmount = salesReturn.TotalAmount,
            SettledAmount = salesReturn.SettledAmount,
            UnsettledAmount = SettlementStateCalculator.UnsettledAmount(salesReturn.TotalAmount, salesReturn.SettledAmount),
            SettlementState = (int)SettlementStateCalculator.Derive(salesReturn.TotalAmount, salesReturn.SettledAmount),
            Status = (int)salesReturn.Status,
            Remark = salesReturn.Remark,
            CreatedBy = salesReturn.CreatedBy?.ToString(),
            CreatedAt = salesReturn.CreatedAt,
            Items = items.Select(i => new SalesReturnItemDto
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
    public static SalesReturnListItemDto ToSalesReturnListItemDto(SalesReturn salesReturn)
        => new()
        {
            Id = salesReturn.Id.ToString(),
            ReturnNo = salesReturn.ReturnNo,
            PartnerId = salesReturn.PartnerId.ToString(),
            PartnerName = salesReturn.PartnerName,
            ReturnDate = salesReturn.ReturnDate,
            TotalAmount = salesReturn.TotalAmount,
            SettledAmount = salesReturn.SettledAmount,
            UnsettledAmount = SettlementStateCalculator.UnsettledAmount(salesReturn.TotalAmount, salesReturn.SettledAmount),
            SettlementState = (int)SettlementStateCalculator.Derive(salesReturn.TotalAmount, salesReturn.SettledAmount),
            Status = (int)salesReturn.Status,
            CreatedAt = salesReturn.CreatedAt,
        };
}
