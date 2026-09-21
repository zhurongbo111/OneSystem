using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.SalesReturns.GetSalesReturnById;

/// <summary>
/// 销售退货单详情查询用例：不存在 → 40400；快照字段原样返回
/// </summary>
public sealed class GetSalesReturnByIdRequestHandler : IRequestHandler<GetSalesReturnByIdRequest, SalesReturnDetailDto>
{
    private readonly ISalesReturnRepository _salesReturnRepository;

    /// <summary>
    /// 初始化销售退货单详情查询用例处理器
    /// </summary>
    public GetSalesReturnByIdRequestHandler(ISalesReturnRepository salesReturnRepository)
    {
        _salesReturnRepository = salesReturnRepository;
    }

    /// <summary>
    /// 处理销售退货单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesReturnDetailDto> HandleAsync(GetSalesReturnByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (salesReturn, items) = await _salesReturnRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (salesReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售退货单不存在");
        }

        return SalesReturnsDtoMapper.ToSalesReturnDetailDto(salesReturn, items);
    }
}
