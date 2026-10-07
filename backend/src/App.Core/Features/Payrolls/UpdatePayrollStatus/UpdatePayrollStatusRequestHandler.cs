using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Payrolls.UpdatePayrollStatus;

/// <summary>
/// 工资单发放 / 反发放用例：存在 → 状态变更（草稿 ⇄ 已发放）→ 落库（与审计日志同事务）。
/// 发放后编辑 / 删除受限（`40171`），反发放是该锁的后门（受 `payroll.status` 权限限制）。
/// </summary>
public sealed class UpdatePayrollStatusRequestHandler : IRequestHandler<UpdatePayrollStatusRequest, PayrollDetailDto>
{
    private readonly IPayrollRepository _payrollRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化工资单发放 / 反发放用例处理器
    /// </summary>
    public UpdatePayrollStatusRequestHandler(
        IPayrollRepository payrollRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _payrollRepository = payrollRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理工资单发放 / 反发放请求
    /// </summary>
    /// <param name="request">状态请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PayrollDetailDto> HandleAsync(UpdatePayrollStatusRequest request, CancellationToken cancellationToken = default)
    {
        var payroll = await _payrollRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "工资单不存在");

        var target = (PayrollStatus)request.Status;

        // 状态未变化：幂等返回当前值，不写库、不留痕
        if (payroll.Status == target)
        {
            return PayrollDtoMapper.ToPayrollDetailDto(payroll);
        }

        var beforeStatus = payroll.Status;
        var now = DateTimeOffset.UtcNow;
        payroll.Status = target;
        payroll.UpdatedAt = now;
        payroll.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _payrollRepository.UpdateAsync(payroll, cancellationToken);

            var period = PayrollPeriod.Text(payroll.Year, payroll.Month);
            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.PayrollStatus(beforeStatus), AuditText.PayrollStatus(payroll.Status));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Payroll,
                Action = AuditAction.StatusChange,
                ResourceId = payroll.Id,
                ResourceNo = payroll.EmployeeName,
                Summary = target == PayrollStatus.Paid
                    ? $"发放工资单 {payroll.EmployeeName}（{period}，实发 {AuditSummary.Money(payroll.NetPay)}）"
                    : $"反发放工资单 {payroll.EmployeeName}（{period}，状态回到草稿）",
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

        return PayrollDtoMapper.ToPayrollDetailDto(payroll);
    }
}
