using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Accounts.DeleteAccount;

/// <summary>
/// 删除会计科目用例：存在性 → 预置科目 / 有子科目 / 被凭证引用（删除保护）→ 物理删除（与审计日志同事务）。
/// 科目停用承载历史语义，删除仅对「非预置的空叶子」开放
/// </summary>
public sealed class DeleteAccountRequestHandler : IRequestHandler<DeleteAccountRequest, object?>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除会计科目用例处理器
    /// </summary>
    public DeleteAccountRequestHandler(
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除会计科目请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeleteAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "科目不存在");

        // 查库约束：预置科目不可删除（标准科目缺失会导致 033 的默认映射失效，允许改名 / 停用）
        if (account.IsPreset)
        {
            throw new BusinessException(ErrorCode.AccountInUse, "预置科目不可删除");
        }

        // 查库约束：有子科目禁止删除（避免产生孤儿子树）
        if (await _accountRepository.HasChildrenAsync(account.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.AccountInUse, "该科目存在子科目，不可删除");
        }

        // 查库约束：已被凭证分录引用禁止删除（凭证表由 033 落地，当前恒不命中）
        if (await _accountRepository.IsReferencedByVoucherAsync(account.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.AccountReferencedByVoucher, "该科目已被凭证引用，不可删除");
        }

        var now = DateTimeOffset.UtcNow;

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _accountRepository.DeleteAsync(account, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "科目编码", account.Code, null)
                .Add("name", "科目名称", account.Name, null);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Account,
                Action = AuditAction.Delete,
                ResourceId = account.Id,
                ResourceNo = account.Code,
                Summary = $"删除科目 {account.Name}（{account.Code}）",
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

        return null;
    }
}