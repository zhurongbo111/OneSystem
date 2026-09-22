using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Invoices.GetInvoicableOrders;

/// <summary>
/// 可开票单据候选查询请求（新建发票页：按往来单位 + 发票类型返回未开票单据）
/// </summary>
public sealed class GetInvoicableOrdersRequest : IRequest<PagedResult<InvoicableOrderDto>>
{
    /// <summary>往来单位 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>发票类型（0 进项 / 1 销项；决定可开票的单据类型集合）</summary>
    public required InvoiceType Type { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}