using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Transfers.GetTransferById;

/// <summary>
/// 调拨单详情查询用例：不存在 → 40400；快照字段原样返回
/// </summary>
public sealed class GetTransferByIdRequestHandler : IRequestHandler<GetTransferByIdRequest, TransferDetailDto>
{
    private readonly ITransferRepository _transferRepository;

    /// <summary>
    /// 初始化调拨单详情查询用例处理器
    /// </summary>
    public GetTransferByIdRequestHandler(ITransferRepository transferRepository)
    {
        _transferRepository = transferRepository;
    }

    /// <summary>
    /// 处理调拨单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TransferDetailDto> HandleAsync(GetTransferByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (transfer, items) = await _transferRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (transfer is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "调拨单不存在");
        }

        return TransfersDtoMapper.ToTransferDetailDto(transfer, items);
    }
}
