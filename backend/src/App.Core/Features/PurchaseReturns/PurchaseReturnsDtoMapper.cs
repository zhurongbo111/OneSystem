using App.Core.Entities;

namespace App.Core.Features.PurchaseReturns;

/// <summary>
/// 采购退货单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API）
/// </summary>
internal static class PurchaseReturnsDtoMapper
{
    /// <summary>
    /// 主表实体 + 明细行实体转详情 DTO
    /// </summary>
    public static PurchaseReturnDetailDto ToPurchaseReturnDetailDto(PurchaseReturn purchaseReturn, IReadOnlyList<PurchaseReturnItem> items)
        => new()
        {
            Id = purchaseReturn.Id.ToString(),
            ReturnNo = purchaseReturn.ReturnNo,
            PartnerId = purchaseReturn.PartnerId.ToString(),
            PartnerName = purchaseReturn.PartnerName,
            ReturnDate = purchaseReturn.ReturnDate,
            TotalAmount = purchaseReturn.TotalAmount,
            SettledAmount = purchaseReturn.SettledAmount,
            UnsettledAmount = SettlementStateCalculator.UnsettledAmount(purchaseReturn.TotalAmount, purchaseReturn.SettledAmount),
            SettlementState = (int)SettlementStateCalculator.Derive(purchaseReturn.TotalAmount, purchaseReturn.SettledAmount),
            Status = (int)purchaseReturn.Status,
            Remark = purchaseReturn.Remark,
            CreatedBy = purchaseReturn.CreatedBy?.ToString(),
            CreatedAt = purchaseReturn.CreatedAt,
            Items = items.Select(i => new PurchaseReturnItemDto
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
    public static PurchaseReturnListItemDto ToPurchaseReturnListItemDto(PurchaseReturn purchaseReturn)
        => new()
        {
            Id = purchaseReturn.Id.ToString(),
            ReturnNo = purchaseReturn.ReturnNo,
            PartnerId = purchaseReturn.PartnerId.ToString(),
            PartnerName = purchaseReturn.PartnerName,
            ReturnDate = purchaseReturn.ReturnDate,
            TotalAmount = purchaseReturn.TotalAmount,
            SettledAmount = purchaseReturn.SettledAmount,
            UnsettledAmount = SettlementStateCalculator.UnsettledAmount(purchaseReturn.TotalAmount, purchaseReturn.SettledAmount),
            SettlementState = (int)SettlementStateCalculator.Derive(purchaseReturn.TotalAmount, purchaseReturn.SettledAmount),
            Status = (int)purchaseReturn.Status,
            CreatedAt = purchaseReturn.CreatedAt,
        };
}
