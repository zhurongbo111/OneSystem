using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Invoices;

/// <summary>
/// 发票出参映射（读模型 / 实体 → DTO，禁止把实体暴露到 API）
/// </summary>
internal static class InvoicesDtoMapper
{
    /// <summary>列表读模型转列表 DTO</summary>
    public static InvoiceListItemDto ToInvoiceListItemDto(InvoiceListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            InvoiceNo = item.InvoiceNo,
            Type = (int)item.Type,
            PartnerName = item.PartnerName,
            InvoiceDate = item.InvoiceDate,
            AmountExcludingTax = item.AmountExcludingTax,
            TaxRate = item.TaxRate,
            TaxAmount = item.TaxAmount,
            TotalAmount = item.TotalAmount,
            Status = (int)item.Status,
            CreatedAt = item.CreatedAt,
            OrderNoSummary = item.OrderNoSummary,
        };

    /// <summary>主表实体 + 关联明细转详情 DTO</summary>
    public static InvoiceDetailDto ToInvoiceDetailDto(Invoice invoice, IReadOnlyList<InvoiceItem> items)
        => new()
        {
            Id = invoice.Id.ToString(),
            InvoiceNo = invoice.InvoiceNo,
            Type = (int)invoice.Type,
            PartnerId = invoice.PartnerId.ToString(),
            PartnerName = invoice.PartnerName,
            InvoiceDate = invoice.InvoiceDate,
            AmountExcludingTax = invoice.AmountExcludingTax,
            TaxRate = invoice.TaxRate,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            Status = (int)invoice.Status,
            Remark = invoice.Remark,
            CreatedBy = invoice.CreatedBy?.ToString(),
            CreatedAt = invoice.CreatedAt,
            Items = items.Select(i => new InvoiceItemDto
            {
                Id = i.Id.ToString(),
                OrderType = (int)i.OrderType,
                OrderId = i.OrderId.ToString(),
                OrderNo = i.OrderNo,
                OrderDate = i.OrderDate,
                OrderTotalAmount = i.OrderTotalAmount,
                Amount = i.Amount,
            }).ToList(),
        };

    /// <summary>可开票候选读模型转 DTO（未开票金额在 Mapper 内推导）</summary>
    public static InvoicableOrderDto ToInvoicableOrderDto(InvoicableOrderItem item)
        => new()
        {
            OrderType = (int)item.OrderType,
            OrderId = item.OrderId.ToString(),
            OrderNo = item.OrderNo,
            OrderDate = item.OrderDate,
            TotalAmount = item.TotalAmount,
            InvoicedAmount = item.InvoicedAmount,
            UninvoicedAmount = item.UninvoicedAmount,
        };
}