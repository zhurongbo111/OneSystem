namespace App.Core.Features.Transfers;

/// <summary>
/// 调拨单列表行出参模型（与后端 DTO camelCase 一一对应；含 Status 供前端作废行置灰）。
/// 调拨不落业务金额（无价格概念），列表只展示「数量合计」与「明细行数」。
/// </summary>
public sealed class TransferListItemDto
{
    /// <summary>调拨单 id</summary>
    public required string Id { get; init; }

    /// <summary>单号</summary>
    public required string TransferNo { get; init; }

    /// <summary>转出仓 id</summary>
    public required string FromWarehouseId { get; init; }

    /// <summary>转出仓名称快照</summary>
    public required string FromWarehouseName { get; init; }

    /// <summary>转入仓 id</summary>
    public required string ToWarehouseId { get; init; }

    /// <summary>转入仓名称快照</summary>
    public required string ToWarehouseName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset TransferDate { get; init; }

    /// <summary>明细行数</summary>
    public required int ItemCount { get; init; }

    /// <summary>数量合计</summary>
    public required int TotalQuantity { get; init; }

    /// <summary>单据状态（0 已作废 / 1 正常）</summary>
    public required int Status { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// 调拨单明细出参模型（快照字段原样返回；不含 BatchId —— 040-erp-batch 落地前恒为空，不进 DTO）。
/// </summary>
public sealed class TransferItemDto
{
    /// <summary>明细行 id</summary>
    public required string Id { get; init; }

    /// <summary>商品 id</summary>
    public required string ProductId { get; init; }

    /// <summary>商品编码快照</summary>
    public required string ProductCode { get; init; }

    /// <summary>商品名称快照</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位快照</summary>
    public required string Unit { get; init; }

    /// <summary>调拨数量</summary>
    public required int Quantity { get; init; }
}

/// <summary>
/// 调拨单详情出参模型（列表字段 + 备注 + 创建人 + 明细行，按插入顺序）。
/// </summary>
public sealed class TransferDetailDto
{
    /// <summary>调拨单 id</summary>
    public required string Id { get; init; }

    /// <summary>单号</summary>
    public required string TransferNo { get; init; }

    /// <summary>转出仓 id</summary>
    public required string FromWarehouseId { get; init; }

    /// <summary>转出仓名称快照</summary>
    public required string FromWarehouseName { get; init; }

    /// <summary>转入仓 id</summary>
    public required string ToWarehouseId { get; init; }

    /// <summary>转入仓名称快照</summary>
    public required string ToWarehouseName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset TransferDate { get; init; }

    /// <summary>明细行数</summary>
    public required int ItemCount { get; init; }

    /// <summary>数量合计</summary>
    public required int TotalQuantity { get; init; }

    /// <summary>单据状态（0 已作废 / 1 正常）</summary>
    public required int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>明细行（按插入顺序）</summary>
    public required IReadOnlyList<TransferItemDto> Items { get; init; }
}
