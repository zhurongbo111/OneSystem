using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Payrolls.DeletePayroll;

/// <summary>
/// 删除工资单用例：存在 → 非草稿拒绝（`40171`，发放锁定）→ 物理删除（与审计日志同事务）
/// </summary>
public sealed class DeletePayrollRequestHandler : IRequestHandler<DeletePayrollRequest, object?>
{
    private readonly IPayrollRepository _payrollRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除工资单用例处理器
    /// </summary>
    public DeletePayrollRequestHandler(
        IPayrollRepository payrollRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _payrollRepository = payrollRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除工资单请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeletePayrollRequest request, CancellationToken cancellationToken = default)
    {
        var payroll = await _payrollRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "工资单不存在");

        // 查库约束：已发放工资单锁定（禁止删除）
        if (payroll.Status != PayrollStatus.Draft)
        {
            throw new BusinessException(ErrorCode.PayrollLocked, "工资单已发放，禁止删除");
        }

        var now = DateTimeOffset.UtcNow;

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _payrollRepository.DeleteAsync(payroll, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("employeeId", "员工", payroll.EmployeeName, null)
                .Add("period", "期间", PayrollPeriod.Text(payroll.Year, payroll.Month), null)
                .Add("netPay", "实发", AuditSummary.Money(payroll.NetPay), null);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Payroll,
                Action = AuditAction.Delete,
                ResourceId = payroll.Id,
                ResourceNo = payroll.EmployeeName,
                Summary = $"删除工资单 {payroll.EmployeeName}（{PayrollPeriod.Text(payroll.Year, payroll.Month)}）",
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
