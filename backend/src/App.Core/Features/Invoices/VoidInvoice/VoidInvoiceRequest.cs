using App.Core.Abstractions;

namespace App.Core.Features.Invoices.VoidInvoice;

/// <summary>
/// 作废发票请求（作废为终态：明细保留，占用金额按聚合自动释放）
/// </summary>
public sealed class VoidInvoiceRequest : IRequest<InvoiceDetailDto>
{
    /// <summary>发票 id</summary>
    public Guid Id { get; init; }
}