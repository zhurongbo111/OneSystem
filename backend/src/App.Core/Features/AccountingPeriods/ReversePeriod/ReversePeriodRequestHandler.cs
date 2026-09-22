using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.AccountingPeriods.ReversePeriod;

/// <summary>
/// 会计期间反结账用例：期间不存在 → 40400；未结账幂等返回（不重复写）；
/// 事务内清状态与结账信息 + 审计日志（反结账后可继续记账 / 作废凭证）
/// </summary>
public sealed class ReversePeriodRequestHandler : IRequestHandler<ReversePeriodRequest, PeriodDto>
{
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化会计期间反结账用例处理器
    /// </summary>
    public ReversePeriodRequestHandler(
        IAccountingPeriodRepository accountingPeriodRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _accountingPeriodRepository = accountingPeriodRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理会计期间反结账请求
    /// </summary>
    /// <param name="request">反结账请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PeriodDto> HandleAsync(ReversePeriodRequest request, CancellationToken cancellationToken = default)
    {
        var period = await _accountingPeriodRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "会计期间不存在");

        if (period.Status == PeriodStatus.Open)
        {
            // 幂等：未结账直接返回，不重复写状态与日志
            return GeneralLedgerDtoMapper.ToPeriodDto(period);
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var label = GeneralLedgerDtoMapper.PeriodLabel(period);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _accountingPeriodRepository.SetStatusAsync(period.Id, PeriodStatus.Open, operatorId, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "期间状态", "已结账", "未结账");
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.AccountingPeriod,
                Action = AuditAction.Update,
                ResourceId = period.Id,
                ResourceNo = label,
                Summary = $"反结账会计期间 {label}",
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

        period.Status = PeriodStatus.Open;
        period.ClosedAt = null;
        period.ClosedBy = null;
        return GeneralLedgerDtoMapper.ToPeriodDto(period);
    }
}
