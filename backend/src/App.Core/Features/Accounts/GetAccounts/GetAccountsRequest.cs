using App.Core.Abstractions;

namespace App.Core.Features.Accounts.GetAccounts;

/// <summary>
/// 会计科目树查询请求（空参数：科目量级小，一次返回全量树，不在服务端分页）
/// </summary>
public sealed class GetAccountsRequest : IRequest<IReadOnlyList<AccountTreeNodeDto>>
{
}