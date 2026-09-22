using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Accounts.UpdateAccountStatus;

/// <summary>
/// 会计科目停用 / 启用用例：存在性 → 更新状态（与审计日志同事务）。
/// 停用科目不可被新凭证引用（`033` 落地后生效），历史数据保留
/// </summary>
public sealed class UpdateAccountStatusRequestHandler : IRequestHandler<UpdateAccountStatusRequest, AccountDetailDto>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化会计科目停用 / 启用用例处理器
    /// </summary>
    public UpdateAccountStatusRequestHandler(
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理会计科目停用 / 启用请求
    /// </summary>
    /// <param name="request">停用 / 启用请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<AccountDetailDto> HandleAsync(UpdateAccountStatusRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "科目不存在");

        var beforeStatus = account.Status;
        var now = DateTimeOffset.UtcNow;
        account.Status = (AccountStatus)request.Status;
        account.UpdatedAt = now;
        account.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _accountRepository.UpdateAsync(account, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.AccountStatus(beforeStatus), AuditText.AccountStatus(account.Status));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Account,
                Action = AuditAction.StatusChange,
                ResourceId = account.Id,
                ResourceNo = account.Code,
                Summary = $"{(account.Status == AccountStatus.Enabled ? "启用" : "停用")}科目 {account.Name}（{account.Code}）",
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

        return AccountDtoMapper.ToAccountDetailDto(account);
    }
}