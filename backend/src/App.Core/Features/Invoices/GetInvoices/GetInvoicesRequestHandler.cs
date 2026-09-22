using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Invoices.GetInvoices;

/// <summary>
/// 发票分页查询用例：仓储分页筛选（关键词含关联单据号、含作废发票）→ 映射 DTO（含 Status 供前端置灰）
/// </summary>
public sealed class GetInvoicesRequestHandler : IRequestHandler<GetInvoicesRequest, PagedResult<InvoiceListItemDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    /// <summary>
    /// 初始化发票分页查询用例处理器
    /// </summary>
    public GetInvoicesRequestHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    /// <summary>
    /// 处理发票分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<InvoiceListItemDto>> HandleAsync(GetInvoicesRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _invoiceRepository.GetPagedAsync(
            request.Keyword,
            request.Type,
            request.PartnerId,
            request.Start,
            request.End,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<InvoiceListItemDto>
        {
            Items = items.Select(InvoicesDtoMapper.ToInvoiceListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}