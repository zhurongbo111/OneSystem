using App.Core.Abstractions;

namespace App.Core.Features.Transfers.VoidTransfer;

/// <summary>
/// 调拨单作废请求（仅改状态，不删数据；双向回冲库存，见 design.md §3.4）
/// </summary>
public sealed class VoidTransferRequest : IRequest<TransferDetailDto>
{
    /// <summary>调拨单 id</summary>
    public required Guid Id { get; init; }
}
