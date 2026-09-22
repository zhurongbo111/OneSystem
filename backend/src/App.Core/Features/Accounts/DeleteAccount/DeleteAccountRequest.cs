using App.Core.Abstractions;

namespace App.Core.Features.Accounts.DeleteAccount;

/// <summary>
/// 删除会计科目请求
/// </summary>
public sealed class DeleteAccountRequest : IRequest<object?>
{
    /// <summary>科目 id</summary>
    public Guid Id { get; init; }
}