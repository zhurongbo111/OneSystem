namespace App.Core.Entities;

/// <summary>
/// 收付款单核销明细实体（对应 PostgreSQL 表 SettlementItems，specs/023-erp-settlement/design.md §2.2）。
/// 明细不软删除、不可改；单据号 / 单据日期 / 单据总额均为开单时快照，详情页免跨四表联查。
/// </summary>
public sealed class SettlementItem
{
    /// <summary>核销明细 ID</summary>
    public Guid Id { get; set; }

    /// <summary>所属收付款单 ID（外键 → Settlements(Id)）</summary>
    public Guid SettlementId { get; set; }

    /// <summary>被核销单据类型</summary>
    public SettlementOrderType OrderType { get; set; }

    /// <summary>被核销单据 id</summary>
    public Guid OrderId { get; set; }

    /// <summary>被核销单据号快照</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>被核销单据日期快照</summary>
    public DateTimeOffset OrderDate { get; set; }

    /// <summary>被核销单据总额快照</summary>
    public decimal OrderTotalAmount { get; set; }

    /// <summary>本次核销金额（&gt; 0）</summary>
    public decimal Amount { get; set; }
}
