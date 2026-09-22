using App.Core.Abstractions;

namespace App.Core.Features.Invoices.GetInvoiceById;

/// <summary>
/// 发票详情查询请求
/// </summary>
public sealed class GetInvoiceByIdRequest : IRequest<InvoiceDetailDto>
{
    /// <summary>发票 id</summary>
    public Guid Id { get; init; }
}