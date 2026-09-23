using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 资金账户余额读模型（余额总览用；余额口径见 specs/034-erp-cash/design.md §0.2）。
/// </summary>
public sealed record BankAccountBalanceItem
{
    /// <summary>资金账户 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>账户编码</summary>
    public required string Code { get; init; }

    /// <summary>账户名称</summary>
    public required string Name { get; init; }

    /// <summary>账户类型</summary>
    public required BankAccountType Type { get; init; }

    /// <summary>账户状态</summary>
    public required BankAccountStatus Status { get; init; }

    /// <summary>当前余额（初始余额 + Σ 收款 − Σ 付款，只计未作废收付款单）</summary>
    public required decimal Balance { get; init; }
}
