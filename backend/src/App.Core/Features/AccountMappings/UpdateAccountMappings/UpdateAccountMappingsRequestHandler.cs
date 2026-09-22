using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.GeneralLedger;
using App.Core.Finance;

namespace App.Core.Features.AccountMappings.UpdateAccountMappings;

/// <summary>
/// 科目映射维护用例（全量覆盖）：
/// 键合法性 → 逐项科目校验（存在 / 末级 / 启用，否则 40157）
/// → 同一事务：逐项 upsert + 审计日志 → 返回全部映射
/// </summary>
public sealed class UpdateAccountMappingsRequestHandler : IRequestHandler<UpdateAccountMappingsRequest, IReadOnlyList<AccountMappingDto>>
{
    private readonly IAccountMappingRepository _accountMappingRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化科目映射维护用例处理器
    /// </summary>
    public UpdateAccountMappingsRequestHandler(
        IAccountMappingRepository accountMappingRepository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _accountMappingRepository = accountMappingRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理科目映射维护请求
    /// </summary>
    /// <param name="request">维护请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<AccountMappingDto>> HandleAsync(UpdateAccountMappingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.Validation, "科目映射不能为空");
        }

        // 双保险：键合法性（Validator 已拦集合不一致，此处拦未登记键）
        foreach (var item in request.Items)
        {
            if (!AccountMappingKeys.All.Any(d => string.Equals(d.Key, item.Key, StringComparison.Ordinal)))
            {
                throw new BusinessException(ErrorCode.Validation, $"未登记的科目映射键：{item.Key}");
            }
        }

        // 查库约束：目标科目须为末级且启用
        var accounts = await _accountRepository.GetAllAsync(cancellationToken);
        var accountsById = accounts.ToDictionary(a => a.Id);
        var nonLeafIds = accounts.Where(a => a.ParentId is not null).Select(a => a.ParentId!.Value).ToHashSet();

        foreach (var item in request.Items)
        {
            if (!accountsById.TryGetValue(item.AccountId, out var account))
            {
                throw new BusinessException(ErrorCode.NotFound, "目标科目不存在");
            }

            if (nonLeafIds.Contains(account.Id) || account.Status != AccountStatus.Enabled)
            {
                throw new BusinessException(
                    ErrorCode.VoucherAccountInvalid,
                    $"科目 {account.Code} {account.Name} 非末级或已停用，不可作为映射科目");
            }
        }

        var existingMappings = await _accountMappingRepository.GetAllAsync(cancellationToken);
        var existingAccountIds = existingMappings.ToDictionary(m => m.Key, m => m.AccountId, StringComparer.Ordinal);

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in request.Items)
            {
                await _accountMappingRepository.UpsertAsync(item.Key, item.AccountId, operatorId, cancellationToken);
            }

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            var changeBuilder = new AuditChangeBuilder();
            foreach (var item in request.Items)
            {
                var label = AccountMappingKeys.LabelOf(item.Key);
                var before = existingAccountIds.TryGetValue(item.Key, out var previousId)
                    && accountsById.TryGetValue(previousId, out var previous)
                        ? $"{previous.Code} {previous.Name}"
                        : null;
                var after = accountsById.TryGetValue(item.AccountId, out var current)
                    ? $"{current.Code} {current.Name}"
                    : null;
                changeBuilder.Add($"mapping.{item.Key}", label, before, after);
            }

            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.AccountMapping,
                Action = AuditAction.Update,
                ResourceId = null,
                ResourceNo = null,
                Summary = $"更新科目映射（{AuditSummary.Count(request.Items.Count)} 个键）",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        // 重新读取并按键顺序返回
        var updated = await _accountMappingRepository.GetAllAsync(cancellationToken);
        var accountIdsByKey = updated.ToDictionary(m => m.Key, m => m.AccountId, StringComparer.Ordinal);

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
