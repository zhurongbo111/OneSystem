using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 往来对账台账读模型（仓储出参契约，按往来单位聚合应收 / 应付；不暴露到 API）。
/// </summary>
public sealed record ReconciliationItem
{
    /// <summary>往来单位 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>往来单位名称</summary>
    public required string PartnerName { get; init; }

    /// <summary>往来单位类型</summary>
    public required PartnerType PartnerType { get; init; }

    /// <summary>应收余额 = Σ 销售出库单未结 + Σ 采购退货单未结（未结 = TotalAmount − SettledAmount，仅计未作废单据）</summary>
    public required decimal ReceivableAmount { get; init; }

    /// <summary>应付余额 = Σ 采购入库单未结 + Σ 销售退货单未结（未结 = TotalAmount − SettledAmount，仅计未作废单据）</summary>
    public required decimal PayableAmount { get; init; }

    /// <summary>未结单据数（四类单据中未结金额 &gt; 0 且未作废的合计）</summary>
    public required int UnsettledOrderCount { get; init; }

    /// <summary>账期天数（0 = 现结；036-erp-partner-price 追加，供到期日推导）</summary>
    public required int PaymentTermDays { get; init; }

    /// <summary>应收侧未结单据的最早到期日（单据日期 + 账期天数）；无未结应收单据时为 null</summary>
    public DateOnly? EarliestDueDate { get; init; }

    /// <summary>最大逾期天数（仅计未结且已过期的应收单据；无逾期时为 0）</summary>
    public required int MaxOverdueDays { get; init; }

    /// <summary>逾期应收单据数（未结且已过期）</summary>
    public required int OverdueOrderCount { get; init; }
}