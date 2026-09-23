namespace App.Core.Features.BankAccounts;

/// <summary>
/// 资金账户余额出参模型（余额总览，含账户数 / 总余额所需的类型区分）
/// </summary>
public sealed class BankAccountBalanceItemDto
{
    /// <summary>资金账户 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>账户编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>账户名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>账户类型（1 现金 / 2 银行）</summary>
    public int Type { get; init; }

    /// <summary>账户状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>当前余额</summary>
    public decimal Balance { get; init; }
}
