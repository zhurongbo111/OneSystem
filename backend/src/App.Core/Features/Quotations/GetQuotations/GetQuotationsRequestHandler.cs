using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Quotations.GetQuotations;

/// <summary>
/// 报价单分页查询用例：仓储分页筛选（含已转订单 / 已作废单）→ 映射 DTO（含明细行数）
/// </summary>
public sealed class GetQuotationsRequestHandler : IRequestHandler<GetQuotationsRequest, PagedResult<QuotationListItemDto>>
{
    private readonly IQuotationRepository _quotationRepository;

    /// <summary>
    /// 初始化报价单分页查询用例处理器
    /// </summary>
    public GetQuotationsRequestHandler(IQuotationRepository quotationRepository)
    {
        _quotationRepository = quotationRepository;
    }

    /// <summary>
    /// 处理报价单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<QuotationListItemDto>> HandleAsync(GetQuotationsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _quotationRepository.GetPagedAsync(
            request.Keyword, request.Status, request.Start, request.End,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<QuotationListItemDto>
        {
            Items = items.Select(QuotationsDtoMapper.ToQuotationListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
