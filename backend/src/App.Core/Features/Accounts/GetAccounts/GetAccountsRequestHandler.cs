using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Accounts.GetAccounts;

/// <summary>
/// 会计科目树用例：仓储一次取全量科目，本用例内存建树（末级科目 = 无子科目），直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetAccountsRequestHandler : IRequestHandler<GetAccountsRequest, IReadOnlyList<AccountTreeNodeDto>>
{
    private readonly IAccountRepository _accountRepository;

    /// <summary>
    /// 初始化会计科目树用例处理器
    /// </summary>
    public GetAccountsRequestHandler(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// 处理会计科目树请求
    /// </summary>
    /// <param name="request">树请求（空参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<AccountTreeNodeDto>> HandleAsync(GetAccountsRequest request, CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetAllAsync(cancellationToken);
        return BuildTree(accounts).Select(AccountDtoMapper.ToAccountTreeNodeDto).ToList();
    }

    /// <summary>
    /// 把全量科目（仓储已按同级排序）组装为树，返回一级科目节点集合
    /// </summary>
    /// <param name="accounts">全量科目</param>
    internal static IReadOnlyList<AccountTreeNode> BuildTree(IReadOnlyList<Account> accounts)
    {
        var childrenByParent = accounts.ToLookup(account => account.ParentId);

        AccountTreeNode Build(Account account)
            => new()
            {
                Id = account.Id,
                Code = account.Code,
                Name = account.Name,
                Category = account.Category,
                Direction = account.Direction,
                ParentId = account.ParentId,
                SortOrder = account.SortOrder,
                IsPreset = account.IsPreset,
                Status = account.Status,
                Remark = account.Remark,
                Children = childrenByParent[account.Id].Select(Build).ToList(),
            };

        return childrenByParent[null].Select(Build).ToList();
    }
}