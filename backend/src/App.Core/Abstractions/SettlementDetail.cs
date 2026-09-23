using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 收付款单详情读模型（列表字段 + 备注 / 创建人；明细行由 <c>GetDetailAsync</c> 另行返回）
/// （specs/034-erp-cash/design.md §2.2）。
/// </summary>
public sealed record SettlementDetail
{
    /// <summary>收付款单 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>单号</summary>
    public required string SettlementNo { get; init; }

    /// <summary>类型（收款 / 付款）</summary>
    public required SettlementType Type { get; init; }

    /// <summary>往来单位 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>往来单位名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset SettlementDate { get; init; }

    /// <summary>总额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>方式</summary>
    public required SettlementMethod Method { get; init; }

    /// <summary>资金账户 id，可空</summary>
    public required Guid? BankAccountId { get; init; }

    /// <summary>资金账户名称（由 BankAccounts 联查带出，可空）</summary>
    public required string? BankAccountName { get; init; }

    /// <summary>单据状态</summary>
    public required OrderStatus Status { get; init; }

    /// <summary>备注，可空</summary>
    public required string? Remark { get; init; }

    /// <summary>创建人用户 id，可空</summary>
    public required Guid? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
