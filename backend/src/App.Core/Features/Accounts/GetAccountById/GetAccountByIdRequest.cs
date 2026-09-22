using App.Core.Abstractions;

namespace App.Core.Features.Accounts.GetAccountById;

/// <summary>
/// 会计科目详情请求
/// </summary>
public sealed class GetAccountByIdRequest : IRequest<AccountDetailDto>
{
    /// <summary>科目 id</summary>
    public Guid Id { get; init; }
}