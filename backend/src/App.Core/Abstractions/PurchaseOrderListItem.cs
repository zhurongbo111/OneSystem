using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 采购单列表读模型（只读投影；Handler 映射为 DTO 返回）
/// </summary>
public sealed record PurchaseOrderListItem
{
    /// <summary>采购单 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>供应商 ID</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>供应商名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>总金额（后端重算值）</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>结算状态</summary>
    public required OrderSettlementStatus SettlementStatus { get; init; }

    /// <summary>单据状态（前端作废行置灰）</summary>
    public required OrderStatus Status { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
