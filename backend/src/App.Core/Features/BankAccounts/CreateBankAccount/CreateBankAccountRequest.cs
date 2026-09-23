using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.BankAccounts.CreateBankAccount;

/// <summary>
/// 新增资金账户请求
/// </summary>
public sealed class CreateBankAccountRequest : IRequest<BankAccountDetailDto>
{
    /// <summary>账户编码（全局唯一）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>账户名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>账户类型（1 现金 / 2 银行）</summary>
    public int Type { get; init; } = (int)BankAccountType.Cash;

    /// <summary>开户行（type = Bank 时必填）</summary>
    public string? BankName { get; init; }

    /// <summary>银行账号（type = Bank 时可填；type = Cash 时忽略）</summary>
    public string? AccountNo { get; init; }

    /// <summary>初始余额（≥ 0）</summary>
    public decimal InitialBalance { get; init; }

    /// <summary>账户状态（0 停用 / 1 启用，默认启用）</summary>
    public int Status { get; init; } = (int)BankAccountStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}
