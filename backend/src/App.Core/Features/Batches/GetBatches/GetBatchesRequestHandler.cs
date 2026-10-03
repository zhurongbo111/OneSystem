using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Batches.GetBatches;

/// <summary>
/// 批次分页查询用例：批次号关键词 / 商品 / 状态 / 仅看近效期或过期筛选，创建时间倒序
/// </summary>
public sealed class GetBatchesRequestHandler : IRequestHandler<GetBatchesRequest, PagedResult<BatchListItemDto>>
{
    private readonly IBatchRepository _batchRepository;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化批次分页查询用例处理器
    /// </summary>
    public GetBatchesRequestHandler(IBatchRepository batchRepository, ISystemClock clock)
    {
        _batchRepository = batchRepository;
        _clock = clock;
    }

    /// <summary>
    /// 处理批次分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<BatchListItemDto>> HandleAsync(GetBatchesRequest request, CancellationToken cancellationToken = default)
    {
        var status = (PartnerStatus?)request.Status;

        // 「今天」取 UTC 日期粒度（过期判定按日，040 §0）
        var today = _clock.Today;

        var (items, total) = await _batchRepository.GetPagedAsync(
            request.Keyword,
            request.ProductId,
            status,
            request.OnlyExpiring,
            today,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<BatchListItemDto>
        {
            Items = items.Select(BatchDtoMapper.ToListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
