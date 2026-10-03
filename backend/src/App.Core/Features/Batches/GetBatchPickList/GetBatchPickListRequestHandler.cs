using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Batches.GetBatchPickList;

/// <summary>
/// 批次下拉查询用例：按商品 + 仓返回启用批次（到期日升序、无到期日最后）+ 该仓可用库存，
/// 并按固定「今天」计算 IsExpired（ExpiryDate &lt; 今天）/ IsNearExpiry（≤ 今天 + NearExpiryDays 且未过期，040 §0）
/// </summary>
public sealed class GetBatchPickListRequestHandler : IRequestHandler<GetBatchPickListRequest, IReadOnlyList<BatchPickDto>>
{
    private readonly IBatchRepository _batchRepository;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化批次下拉查询用例处理器
    /// </summary>
    public GetBatchPickListRequestHandler(IBatchRepository batchRepository, ISystemClock clock)
    {
        _batchRepository = batchRepository;
        _clock = clock;
    }

    /// <summary>
    /// 处理批次下拉查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<BatchPickDto>> HandleAsync(GetBatchPickListRequest request, CancellationToken cancellationToken = default)
    {
        // 判定集中（040 §1）：过期 / 近效期在 Handler 按固定「今天」计算；仓储的「有库存或未过期」筛选同样基于该「今天」
        var today = _clock.Today;
        var nearExpiryEnd = today.AddDays(BatchFieldConstraints.NearExpiryDays);

        var items = await _batchRepository.GetPickListAsync(
            request.ProductId, request.WarehouseId, today, cancellationToken);

        return items
            .Select(item =>
            {
                var isExpired = item.ExpiryDate is not null && item.ExpiryDate.Value < today;
                var isNearExpiry = !isExpired
                    && item.ExpiryDate is not null
                    && item.ExpiryDate.Value <= nearExpiryEnd;

                return BatchDtoMapper.ToPickDto(item with
                {
                    IsExpired = isExpired,
                    IsNearExpiry = isNearExpiry,
                });
            })
            .ToList();
    }
}
