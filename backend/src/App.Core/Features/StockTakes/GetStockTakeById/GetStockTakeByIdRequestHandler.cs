using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.StockTakes.GetStockTakeById;

/// <summary>
/// 盘点单详情查询用例：不存在 → 40400；快照字段原样返回
/// </summary>
public sealed class GetStockTakeByIdRequestHandler : IRequestHandler<GetStockTakeByIdRequest, StockTakeDetailDto>
{
    private readonly IStockTakeRepository _stockTakeRepository;

    /// <summary>
    /// 初始化盘点单详情查询用例处理器
    /// </summary>
    public GetStockTakeByIdRequestHandler(IStockTakeRepository stockTakeRepository)
    {
        _stockTakeRepository = stockTakeRepository;
    }

    /// <summary>
    /// 处理盘点单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<StockTakeDetailDto> HandleAsync(GetStockTakeByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (take, items) = await _stockTakeRepository.GetDetailAsync(request.Id, cancellationToken);
        if (take is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "盘点单不存在");
        }

        return StockTakeDtoMapper.ToStockTakeDetailDto(take, items);
    }
}
