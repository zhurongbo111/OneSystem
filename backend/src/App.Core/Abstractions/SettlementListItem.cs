using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 收付款单列表项读模型（字段与 <see cref="Settlement"/> 1:1，另加联查列 <see cref="BankAccountName"/>）。
/// 建立原因：仓储出参契约与 API 契约分离，且列表需带出资金账户名称（只存账户 id）
/// （specs/034-erp-cash/design.md §2.2）。
/// </summary>
public sealed record SettlementListItem
{
    /// <summary>收付款单 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>单号（收款 RC / 付款 PY + yyyyMMdd + 4 位序号）</summary>
    public required string SettlementNo { get; init; }

    /// <summary>类型（收款 / 付款）</summary>
    public required SettlementType Type { get; init; }

    /// <summary>往来单位 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>往来单位名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset SettlementDate { get; init; }

    /// <summary>总额（= Σ 核销金额，后端重算值）</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>方式（现金 / 银行转账 / 其他）</summary>
    public required SettlementMethod Method { get; init; }

    /// <summary>资金账户 id，可空（`Other` 结算方式不关联账户）</summary>
    public required Guid? BankAccountId { get; init; }

    /// <summary>资金账户名称（由 BankAccounts 联查带出；未关联账户或账户已删除时为 null）</summary>
    public required string? BankAccountName { get; init; }

    /// <summary>单据状态（正常 / 已作废）</summary>
    public required OrderStatus Status { get; init; }

    /// <summary>创建人用户 id，可空（erp-export 导出「创建人」列用）</summary>
    public required Guid? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
