using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.SalesReturns.GetSalesReturns;

/// <summary>
/// 销售退货单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）
/// </summary>
public sealed class GetSalesReturnsRequestHandler : IRequestHandler<GetSalesReturnsRequest, PagedResult<SalesReturnListItemDto>>
{
    private readonly ISalesReturnRepository _salesReturnRepository;

    /// <summary>
    /// 初始化销售退货单分页查询用例处理器
    /// </summary>
    public GetSalesReturnsRequestHandler(ISalesReturnRepository salesReturnRepository)
    {
        _salesReturnRepository = salesReturnRepository;
    }

    /// <summary>
    /// 处理销售退货单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<SalesReturnListItemDto>> HandleAsync(GetSalesReturnsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _salesReturnRepository.GetPagedAsync(
            request.Keyword, request.PartnerId, request.Start, request.End, request.Settlement,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<SalesReturnListItemDto>
        {
            Items = items.Select(SalesReturnsDtoMapper.ToSalesReturnListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
