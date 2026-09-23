using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.BankAccounts.UpdateBankAccountStatus;

/// <summary>
/// 资金账户启用 / 停用用例：存在性 → 更新状态（与审计日志同事务）。
/// 停用账户不可被新收付款单引用，历史数据保留
/// </summary>
public sealed class UpdateBankAccountStatusRequestHandler : IRequestHandler<UpdateBankAccountStatusRequest, BankAccountDetailDto>
{
    private readonly IBankAccountRepository _bankAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化资金账户启停用例处理器
    /// </summary>
    public UpdateBankAccountStatusRequestHandler(
        IBankAccountRepository bankAccountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _bankAccountRepository = bankAccountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理资金账户启停请求
    /// </summary>
    /// <param name="request">启停请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BankAccountDetailDto> HandleAsync(UpdateBankAccountStatusRequest request, CancellationToken cancellationToken = default)
    {
        var bankAccount = await _bankAccountRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "资金账户不存在");

        var beforeStatus = bankAccount.Status;
        var now = DateTimeOffset.UtcNow;
        bankAccount.Status = (BankAccountStatus)request.Status;
        bankAccount.UpdatedAt = now;
        bankAccount.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _bankAccountRepository.UpdateAsync(bankAccount, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.BankAccountStatus(beforeStatus), AuditText.BankAccountStatus(bankAccount.Status));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.BankAccount,
                Action = AuditAction.StatusChange,
                ResourceId = bankAccount.Id,
                ResourceNo = bankAccount.Code,
                Summary = $"{(bankAccount.Status == BankAccountStatus.Enabled ? "启用" : "停用")}资金账户 {bankAccount.Name}（{bankAccount.Code}）",
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

        return BankAccountDtoMapper.ToBankAccountDetailDto(bankAccount);
    }
}
