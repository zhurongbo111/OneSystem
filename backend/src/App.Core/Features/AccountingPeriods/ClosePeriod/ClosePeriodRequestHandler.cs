using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.AccountingPeriods.ClosePeriod;

/// <summary>
/// 会计期间结账用例：期间不存在 → 40400；已结账幂等返回（不重复写）；
/// 事务内改状态 + 结账信息 + 审计日志
/// </summary>
public sealed class ClosePeriodRequestHandler : IRequestHandler<ClosePeriodRequest, PeriodDto>
{
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化会计期间结账用例处理器
    /// </summary>
    public ClosePeriodRequestHandler(
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
    /// 处理会计期间结账请求
    /// </summary>
    /// <param name="request">结账请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PeriodDto> HandleAsync(ClosePeriodRequest request, CancellationToken cancellationToken = default)
    {
        var period = await _accountingPeriodRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "会计期间不存在");

        if (period.Status == PeriodStatus.Closed)
        {
            // 幂等：已结账直接返回，不重复写状态与日志
            return GeneralLedgerDtoMapper.ToPeriodDto(period);
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var label = GeneralLedgerDtoMapper.PeriodLabel(period);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _accountingPeriodRepository.SetStatusAsync(period.Id, PeriodStatus.Closed, operatorId, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "期间状态", "未结账", "已结账");
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.AccountingPeriod,
                Action = AuditAction.Close,
                ResourceId = period.Id,
                ResourceNo = label,
                Summary = $"结账会计期间 {label}",
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

        period.Status = PeriodStatus.Closed;
        period.ClosedAt = now;
        period.ClosedBy = operatorId;
        return GeneralLedgerDtoMapper.ToPeriodDto(period);
    }
}
