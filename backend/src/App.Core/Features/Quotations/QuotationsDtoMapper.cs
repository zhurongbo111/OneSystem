using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Quotations;

/// <summary>
/// 报价单实体 → 出参映射（实体字段直接映射为 DTO，禁止把实体暴露到 API；派生字段在 Mapper 内计算）。
/// </summary>
internal static class QuotationsDtoMapper
{
    /// <summary>
    /// 主表实体 + 明细行实体转详情 DTO
    /// </summary>
    public static QuotationDetailDto ToQuotationDetailDto(Quotation quotation, IReadOnlyList<QuotationItem> items)
        => new()
        {
            Id = quotation.Id.ToString(),
            QuotationNo = quotation.QuotationNo,
            PartnerId = quotation.PartnerId.ToString(),
            PartnerName = quotation.PartnerName,
            QuotationDate = quotation.QuotationDate,
            ValidUntil = quotation.ValidUntil,
            TotalAmount = quotation.TotalAmount,
            Status = (int)quotation.Status,
            ConvertedOrderId = quotation.ConvertedOrderId?.ToString(),
            ConvertedOrderNo = quotation.ConvertedOrderNo,
            Remark = quotation.Remark,
            CreatedBy = quotation.CreatedBy?.ToString(),
            CreatedAt = quotation.CreatedAt,
            Items = items.Select(ToQuotationItemDto).ToList(),
        };

    /// <summary>
    /// 明细行实体转明细 DTO
    /// </summary>
    public static QuotationItemDto ToQuotationItemDto(QuotationItem item)
        => new()
        {
            Id = item.Id.ToString(),
            ProductId = item.ProductId.ToString(),
            ProductName = item.ProductName,
            Unit = item.Unit,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Subtotal = item.Subtotal,
        };

    /// <summary>
    /// 读模型（主表 + 明细行数）转列表 DTO
    /// </summary>
    public static QuotationListItemDto ToQuotationListItemDto(QuotationListItem item)
        => new()
        {
            Id = item.Quotation.Id.ToString(),
            QuotationNo = item.Quotation.QuotationNo,
            PartnerId = item.Quotation.PartnerId.ToString(),
            PartnerName = item.Quotation.PartnerName,
            QuotationDate = item.Quotation.QuotationDate,
            ValidUntil = item.Quotation.ValidUntil,
            TotalAmount = item.Quotation.TotalAmount,
            ItemCount = item.ItemCount,
            Status = (int)item.Quotation.Status,
            ConvertedOrderNo = item.Quotation.ConvertedOrderNo,
            CreatedAt = item.Quotation.CreatedAt,
        };

    /// <summary>
    /// 转单结果（销售订单 id / 单号）转转单出参
    /// </summary>
    public static ConvertQuotationResultDto ToConvertQuotationResultDto(SalesOrder order)
        => new()
        {
            OrderId = order.Id.ToString(),
            OrderNo = order.OrderNo,
        };
}
