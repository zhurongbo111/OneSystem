using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Transfers.GetTransfers;

/// <summary>
/// 调拨单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）。
/// </summary>
public sealed class GetTransfersRequestHandler : IRequestHandler<GetTransfersRequest, PagedResult<TransferListItemDto>>
{
    private readonly ITransferRepository _transferRepository;

    /// <summary>
    /// 初始化调拨单分页查询用例处理器
    /// </summary>
    public GetTransfersRequestHandler(ITransferRepository transferRepository)
    {
        _transferRepository = transferRepository;
    }

    /// <summary>
    /// 处理调拨单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<TransferListItemDto>> HandleAsync(GetTransfersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _transferRepository.GetPagedAsync(
            request.Keyword,
            request.FromWarehouseId,
            request.ToWarehouseId,
            request.Start,
            request.End,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<TransferListItemDto>
        {
            Items = items.Select(TransfersDtoMapper.ToTransferListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
