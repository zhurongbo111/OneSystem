using App.Core.Abstractions;

namespace App.Core.Features.BankAccounts.GetBankAccountById;

/// <summary>
/// 资金账户详情请求（id 取自路由）
/// </summary>
public sealed class GetBankAccountByIdRequest : IRequest<BankAccountDetailDto>
{
    /// <summary>资金账户 id</summary>
    public Guid Id { get; init; }
}
