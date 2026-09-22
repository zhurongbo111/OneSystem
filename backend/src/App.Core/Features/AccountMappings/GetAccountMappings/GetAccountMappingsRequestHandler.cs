using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;
using App.Core.Finance;

namespace App.Core.Features.AccountMappings.GetAccountMappings;

/// <summary>
/// 科目映射查询用例：按 <see cref="AccountMappingKeys.All"/> 定义的键顺序返回全部映射
/// （未配置的键科目字段为空串，前端据此提示缺失）
/// </summary>
public sealed class GetAccountMappingsRequestHandler : IRequestHandler<GetAccountMappingsRequest, IReadOnlyList<AccountMappingDto>>
{
    private readonly IAccountMappingRepository _accountMappingRepository;
    private readonly IAccountRepository _accountRepository;

    /// <summary>
    /// 初始化科目映射查询用例处理器
    /// </summary>
    public GetAccountMappingsRequestHandler(
        IAccountMappingRepository accountMappingRepository,
        IAccountRepository accountRepository)
    {
        _accountMappingRepository = accountMappingRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// 处理科目映射查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<AccountMappingDto>> HandleAsync(GetAccountMappingsRequest request, CancellationToken cancellationToken = default)
    {
        var mappings = await _accountMappingRepository.GetAllAsync(cancellationToken);
        var accounts = await _accountRepository.GetAllAsync(cancellationToken);

        var accountIdsByKey = mappings.ToDictionary(m => m.Key, m => m.AccountId, StringComparer.Ordinal);
        var accountsById = accounts.ToDictionary(a => a.Id);

        return AccountMappingKeys.All
            .Select(definition =>
            {
                var account = accountIdsByKey.TryGetValue(definition.Key, out var accountId)
                    && accountsById.TryGetValue(accountId, out var found)
                        ? found
                        : null;
                return GeneralLedgerDtoMapper.ToAccountMappingDto(definition.Key, account);
            })
            .ToList();
    }
}
