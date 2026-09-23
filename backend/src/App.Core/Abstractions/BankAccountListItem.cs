using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 资金账户列表项读模型（含派生列 <see cref="Balance"/>：初始余额 + Σ 收款 − Σ 付款，由收付款单聚合而非落列）
/// （specs/034-erp-cash/design.md §3.1）。
/// </summary>
public sealed record BankAccountListItem
{
    /// <summary>资金账户 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>账户编码</summary>
    public required string Code { get; init; }

    /// <summary>账户名称</summary>
    public required string Name { get; init; }

    /// <summary>账户类型</summary>
    public required BankAccountType Type { get; init; }

    /// <summary>开户行，可空</summary>
    public required string? BankName { get; init; }

    /// <summary>银行账号，可空</summary>
    public required string? AccountNo { get; init; }

    /// <summary>初始余额</summary>
    public required decimal InitialBalance { get; init; }

    /// <summary>账户状态</summary>
    public required BankAccountStatus Status { get; init; }

    /// <summary>备注，可空</summary>
    public required string? Remark { get; init; }

    /// <summary>当前余额（初始余额 + Σ 收款 − Σ 付款，只计未作废收付款单）</summary>
    public required decimal Balance { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
