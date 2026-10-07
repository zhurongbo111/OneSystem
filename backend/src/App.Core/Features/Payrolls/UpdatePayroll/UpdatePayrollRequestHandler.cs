using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Payrolls.UpdatePayroll;

/// <summary>
/// 编辑工资单用例：存在 → 非草稿拒绝（`40171`，发放锁定）→ 重算实发 → 更新（与审计日志同事务）。
/// 员工与期间不可改（请求体不含）。
/// </summary>
public sealed class UpdatePayrollRequestHandler : IRequestHandler<UpdatePayrollRequest, PayrollDetailDto>
{
    private readonly IPayrollRepository _payrollRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑工资单用例处理器
    /// </summary>
    public UpdatePayrollRequestHandler(
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
    /// 处理编辑工资单请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PayrollDetailDto> HandleAsync(UpdatePayrollRequest request, CancellationToken cancellationToken = default)
    {
        var payroll = await _payrollRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "工资单不存在");

        // 查库约束：已发放工资单锁定（禁止修改）
        if (payroll.Status != PayrollStatus.Draft)
        {
            throw new BusinessException(ErrorCode.PayrollLocked, "工资单已发放，禁止修改");
        }

        var beforeBaseSalary = payroll.BaseSalary;
        var beforeAllowance = payroll.Allowance;
        var beforeDeduction = payroll.Deduction;
        var beforeNetPay = payroll.NetPay;
        var beforeRemark = payroll.Remark;

        var now = DateTimeOffset.UtcNow;
        payroll.BaseSalary = request.BaseSalary;
        payroll.Allowance = request.Allowance;
        payroll.Deduction = request.Deduction;
        payroll.NetPay = PayrollAmountCalculator.NetPay(request.BaseSalary, request.Allowance, request.Deduction);
        payroll.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        payroll.UpdatedAt = now;
        payroll.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _payrollRepository.UpdateAsync(payroll, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("baseSalary", "基本工资", AuditSummary.Money(beforeBaseSalary), AuditSummary.Money(payroll.BaseSalary))
                .Add("allowance", "津贴", AuditSummary.Money(beforeAllowance), AuditSummary.Money(payroll.Allowance))
                .Add("deduction", "扣款", AuditSummary.Money(beforeDeduction), AuditSummary.Money(payroll.Deduction))
                .Add("netPay", "实发", AuditSummary.Money(beforeNetPay), AuditSummary.Money(payroll.NetPay))
                .Add("remark", "备注", beforeRemark, payroll.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Payroll,
                Action = AuditAction.Update,
                ResourceId = payroll.Id,
                ResourceNo = payroll.EmployeeName,
                Summary = $"修改工资单 {payroll.EmployeeName}（{PayrollPeriod.Text(payroll.Year, payroll.Month)}，实发 {AuditSummary.Money(payroll.NetPay)}）",
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
