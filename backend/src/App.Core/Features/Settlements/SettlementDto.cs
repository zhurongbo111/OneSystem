namespace App.Core.Features.Settlements;

/// <summary>
/// 收付款单出参共享模型（主表 + 核销明细，与后端 DTO camelCase 一一对应；列表行用 SettlementListItemDto）
/// </summary>
public sealed class SettlementDetailDto
{
    /// <summary>收付款单 id</summary>
    public required string Id { get; init; }

    /// <summary>单号（收款 RC / 付款 PY + yyyyMMdd + 4 位序号）</summary>
    public required string SettlementNo { get; init; }

    /// <summary>类型（0 收款 / 1 付款）</summary>
    public required int Type { get; init; }

    /// <summary>往来单位 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>往来单位名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset SettlementDate { get; init; }

    /// <summary>总额（= Σ 核销金额，后端重算值）</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>方式（0 现金 / 1 银行转账 / 2 其他）</summary>
    public required int Method { get; init; }

    /// <summary>单据状态（0 已作废 / 1 正常）</summary>
    public required int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>核销明细（按插入顺序，快照透传）</summary>
    public required IReadOnlyList<SettlementItemDto> Items { get; init; }
}

/// <summary>收付款单核销明细出参模型（单据号 / 日期 / 总额为开单时快照）</summary>
public sealed class SettlementItemDto
{
    /// <summary>核销明细 id</summary>
    public required string Id { get; init; }

    /// <summary>被核销单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required int OrderType { get; init; }

    /// <summary>被核销单据 id</summary>
    public required string OrderId { get; init; }

    /// <summary>被核销单据号快照</summary>
    public required string OrderNo { get; init; }

    /// <summary>被核销单据日期快照</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>被核销单据总额快照</summary>
    public required decimal OrderTotalAmount { get; init; }

    /// <summary>本次核销金额</summary>
    public required decimal Amount { get; init; }
}

/// <summary>收付款单列表行出参模型（含 Status 供前端作废行置灰）</summary>
public sealed class SettlementListItemDto
{
    /// <summary>收付款单 id</summary>
    public required string Id { get; init; }

    /// <summary>单号</summary>
    public required string SettlementNo { get; init; }

    /// <summary>类型（0 收款 / 1 付款）</summary>
    public required int Type { get; init; }

    /// <summary>往来单位 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>往来单位名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset SettlementDate { get; init; }

    /// <summary>总额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>方式（0 现金 / 1 银行转账 / 2 其他）</summary>
    public required int Method { get; init; }

    /// <summary>单据状态（0 已作废 / 1 正常）</summary>
    public required int Status { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>本次核销金额（仅按被核销单据反查时返回；普通列表为 null）</summary>
    public decimal? OrderAmount { get; init; }

    /// <summary>
    /// 该单核销明细的被核销单据类型集合（去重升序，0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）。
    /// 由核销明细派生（不落列）：一张单可混合核销多类单据，故为集合；业务类型 = Type × OrderType（design.md §0）。
    /// </summary>
    public required IReadOnlyList<int> OrderTypes { get; init; }
}

/// <summary>未结单据候选出参模型（新建收付款单页选择核销单据用）</summary>
public sealed class SettlementCandidateDto
{
    /// <summary>被核销单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required int OrderType { get; init; }

    /// <summary>被核销单据 id</summary>
    public required string OrderId { get; init; }

    /// <summary>被核销单据号</summary>
    public required string OrderNo { get; init; }

    /// <summary>被核销单据业务日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>单据总额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>已结算金额</summary>
    public required decimal SettledAmount { get; init; }

    /// <summary>未结金额（= 总额 − 已结算金额，推导值）</summary>
    public required decimal UnsettledAmount { get; init; }
}

/// <summary>往来对账台账行出参模型</summary>
public sealed class ReconciliationListItemDto
{
    /// <summary>往来单位 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>往来单位名称</summary>
    public required string PartnerName { get; init; }

    /// <summary>往来单位类型（1 供应商 / 2 客户 / 3 两者）</summary>
    public required int PartnerType { get; init; }

    /// <summary>应收余额 = Σ 销售单总额 − Σ 销售退货总额 − Σ 已收</summary>
    public required decimal ReceivableAmount { get; init; }

    /// <summary>应付余额 = Σ 采购单总额 − Σ 采购退货总额 − Σ 已付</summary>
    public required decimal PayableAmount { get; init; }

    /// <summary>未结单据数（四类单据中未结且未作废的合计）</summary>
    public required int UnsettledOrderCount { get; init; }
}
