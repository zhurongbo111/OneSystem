using App.Core.Abstractions;

namespace App.Core.Features.Transfers.CreateTransfer;

/// <summary>
/// 新增调拨单请求（一步式：保存即生效，转出仓 −、转入仓 + 同一事务）。
/// 调拨不落业务金额（无价格概念）；数量合计 / 明细行数由后端重算。
/// </summary>
public sealed class CreateTransferRequest : IRequest<TransferDetailDto>
{
    /// <summary>转出仓 id（必填，不允许为空；与转入仓必须不同，相同 → 40126 由 Handler 抛）</summary>
    public required Guid FromWarehouseId { get; init; }

    /// <summary>转入仓 id（必填，不允许为空）</summary>
    public required Guid ToWarehouseId { get; init; }

    /// <summary>业务日期（UTC 午夜，前端所选日期的本地 0 点转 UTC ISO 串）</summary>
    public required DateTimeOffset TransferDate { get; init; }

    /// <summary>明细行（1–100 行；productId / quantity）</summary>
    public required IReadOnlyList<CreateTransferItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>调拨单明细行入参（快照字段由后端从商品档案带出）</summary>
public sealed class CreateTransferItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>调拨数量（≥ 1）</summary>
    public required int Quantity { get; init; }
}
