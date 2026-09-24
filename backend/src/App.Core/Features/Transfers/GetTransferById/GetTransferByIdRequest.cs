using App.Core.Abstractions;

namespace App.Core.Features.Transfers.GetTransferById;

/// <summary>
/// 调拨单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetTransferByIdRequest : IRequest<TransferDetailDto>
{
    /// <summary>调拨单 id</summary>
    public required Guid Id { get; init; }
}
