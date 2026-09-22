using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.GeneralLedger;
using App.Core.Finance;

namespace App.Core.Features.Vouchers.VoidVoucher;

/// <summary>
/// 凭证作废用例：不存在 → 40400；已作废 → 40104（幂等防重）；
/// 归属期间已结账 → 40154；事务内改状态 + 审计（仅改状态，不删数据；余额随之回退）
/// </summary>
public sealed class VoidVoucherRequestHandler : IRequestHandler<VoidVoucherRequest, VoucherDetailDto>
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化凭证作废用例处理器
    /// </summary>
    public VoidVoucherRequestHandler(
        IVoucherRepository voucherRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _voucherRepository = voucherRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理凭证作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<VoucherDetailDto> HandleAsync(VoidVoucherRequest request, CancellationToken cancellationToken = default)
    {
        var (voucher, entries) = await _voucherRepository.GetDetailAsync(request.Id, cancellationToken);
        if (voucher is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "凭证不存在");
        }

        if (voucher.Status == VoucherStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废 / 重复回退余额）
            throw new BusinessException(ErrorCode.OrderVoided, "凭证已作废，禁止再操作");
        }

        // 期间控制：已结账期间禁止作废凭证
        var period = await _accountingPeriodRepository.GetByYearMonthAsync(
            voucher.VoucherDate.Year,
            voucher.VoucherDate.Month,
            cancellationToken);
        if (period is not null && period.Status == PeriodStatus.Closed)
        {
            throw new BusinessException(
                ErrorCode.PeriodClosed,
                $"会计期间 {period.Year}-{period.Month:00} 已结账，禁止作废凭证");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _voucherRepository.VoidAsync(voucher.Id, operatorId, cancellationToken);

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "凭证状态", "已过账", "已作废")
                .Add("totalDebit", "借方合计", null, AuditSummary.Money(voucher.TotalDebit));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Voucher,
                Action = AuditAction.Void,
                ResourceId = voucher.Id,
                ResourceNo = voucher.VoucherNo,
                Summary = $"作废凭证 {voucher.VoucherNo}（来源：{VoucherFactory.SourceTypeLabel(voucher.SourceType)}、{AuditSummary.Money(voucher.TotalDebit)}）",
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

        var (updated, updatedEntries) = await _voucherRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "凭证不存在");
        }

        return GeneralLedgerDtoMapper.ToVoucherDetailDto(updated, updatedEntries);
    }
}
