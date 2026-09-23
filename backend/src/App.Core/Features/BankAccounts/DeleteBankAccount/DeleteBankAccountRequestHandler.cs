using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.BankAccounts.DeleteBankAccount;

/// <summary>
/// 删除资金账户用例：存在性 → 删除保护（被收付款单引用 40161）→ 物理删除（与审计日志同事务）。
/// 停用承载历史语义；删除只对未被任何收付款单引用的账户开放（specs/034-erp-cash/design.md §0.3）
/// </summary>
public sealed class DeleteBankAccountRequestHandler : IRequestHandler<DeleteBankAccountRequest, object?>
{
    private readonly IBankAccountRepository _bankAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除资金账户用例处理器
    /// </summary>
    public DeleteBankAccountRequestHandler(
        IBankAccountRepository bankAccountRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _bankAccountRepository = bankAccountRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除资金账户请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeleteBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        var bankAccount = await _bankAccountRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "资金账户不存在");

        // 查库约束：已被收付款单引用（含作废单，作废单仍属历史资金流水）→ 禁止删除
        if (await _bankAccountRepository.IsReferencedAsync(bankAccount.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.BankAccountInUse, "资金账户已被收付款单引用，禁止删除");
        }

        var now = DateTimeOffset.UtcNow;

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _bankAccountRepository.DeleteAsync(bankAccount, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "账户编码", bankAccount.Code, null)
                .Add("name", "账户名称", bankAccount.Name, null);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.BankAccount,
                Action = AuditAction.Delete,
                ResourceId = bankAccount.Id,
                ResourceNo = bankAccount.Code,
                Summary = $"删除资金账户 {bankAccount.Name}（{bankAccount.Code}）",
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
