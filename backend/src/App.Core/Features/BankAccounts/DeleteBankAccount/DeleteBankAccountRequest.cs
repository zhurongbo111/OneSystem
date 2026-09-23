using App.Core.Abstractions;

namespace App.Core.Features.BankAccounts.DeleteBankAccount;

/// <summary>
/// 删除资金账户请求（id 取自路由）
/// </summary>
public sealed class DeleteBankAccountRequest : IRequest<object?>
{
    /// <summary>资金账户 id</summary>
    public Guid Id { get; init; }
}
