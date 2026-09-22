using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Invoices.GetInvoicableOrders;

/// <summary>
/// 可开票单据候选查询用例：按发票类型推导可开票单据类型集合后，委托跨表只读仓储返回未开票单据
/// （进项 → 采购入库单 + 采购退货单；销项 → 销售出库单 + 销售退货单）
/// </summary>
public sealed class GetInvoicableOrdersRequestHandler : IRequestHandler<GetInvoicableOrdersRequest, PagedResult<InvoicableOrderDto>>
{
    private readonly IInvoiceQueryRepository _invoiceQueryRepository;

    /// <summary>
    /// 初始化可开票单据候选查询用例处理器
    /// </summary>
    public GetInvoicableOrdersRequestHandler(IInvoiceQueryRepository invoiceQueryRepository)
    {
        _invoiceQueryRepository = invoiceQueryRepository;
    }

    /// <summary>
    /// 处理可开票单据候选查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<InvoicableOrderDto>> HandleAsync(GetInvoicableOrdersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _invoiceQueryRepository.GetInvoicableAsync(
            request.PartnerId, request.Type, request.Page, request.PageSize, cancellationToken);

        return new PagedResult<InvoicableOrderDto>
        {
            Items = items.Select(InvoicesDtoMapper.ToInvoicableOrderDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}