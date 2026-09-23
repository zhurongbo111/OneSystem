using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Settlements.CreateSettlement;

/// <summary>
/// 新增收付款单请求（核销即生效：核销明细累加各单据已结算金额）。
/// 总额不在此请求中——后端按 Σ 核销金额重算，不信任前端传值（见 design.md §1）。
/// </summary>
public sealed class CreateSettlementRequest : IRequest<SettlementDetailDto>
{
    /// <summary>类型（0 收款 / 1 付款）</summary>
    public required SettlementType Type { get; init; }

    /// <summary>往来单位 id（收款为客户、付款为供应商）</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>业务日期（UTC 午夜，前端所选日期的本地 0 点转 UTC ISO 串）</summary>
    public required DateTimeOffset SettlementDate { get; init; }

    /// <summary>方式（0 现金 / 1 银行转账 / 2 其他）</summary>
    public required SettlementMethod Method { get; init; }

    /// <summary>
    /// 资金账户 id（034-erp-cash，可空）：现金 ↔ 现金账户、银行转账 ↔ 银行账户，类型不匹配返回 40162；
    /// `Other` 结算方式不关联账户
    /// </summary>
    public Guid? BankAccountId { get; init; }

    /// <summary>核销明细行（1–100 行；orderType / orderId / amount）</summary>
    public required IReadOnlyList<CreateSettlementItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>核销明细行入参（单据号 / 日期 / 总额快照由后端从被核销单据带出）</summary>
public sealed class CreateSettlementItem
{
    /// <summary>被核销单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required SettlementOrderType OrderType { get; init; }

    /// <summary>被核销单据 id</summary>
    public required Guid OrderId { get; init; }

    /// <summary>本次核销金额（&gt; 0，且不超过该单据未结金额）</summary>
    public required decimal Amount { get; init; }
}
