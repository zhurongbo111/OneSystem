using App.Core.Abstractions;

namespace App.Core.Features.Accounts.UpdateAccountStatus;

/// <summary>
/// 会计科目停用 / 启用请求（科目只停用不删除，保留历史与凭证引用）
/// </summary>
public sealed class UpdateAccountStatusRequest : IRequest<AccountDetailDto>
{
    /// <summary>科目 id</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }
}