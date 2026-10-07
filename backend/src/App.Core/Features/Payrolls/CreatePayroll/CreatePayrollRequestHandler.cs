using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Payrolls.CreatePayroll;

/// <summary>
/// 新增工资单用例：员工存在 → 期间唯一（`40170`，一个员工一个月一条）→ 计算实发 → 落库（与审计日志同事务）
/// </summary>
public sealed class CreatePayrollRequestHandler : IRequestHandler<CreatePayrollRequest, PayrollDetailDto>
{
    private readonly IPayrollRepository _payrollRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增工资单用例处理器
    /// </summary>
    public CreatePayrollRequestHandler(
        IPayrollRepository payrollRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _payrollRepository = payrollRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增工资单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PayrollDetailDto> HandleAsync(CreatePayrollRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "员工不存在");

        // 查库约束：该员工该期间只能有一条工资单
        if (await _payrollRepository.ExistsAsync(employee.Id, request.Year, request.Month, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PayrollExists, "该员工该期间的工资单已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var netPay = PayrollAmountCalculator.NetPay(request.BaseSalary, request.Allowance, request.Deduction);
        var payroll = new Payroll
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Year = request.Year,
            Month = request.Month,
            BaseSalary = request.BaseSalary,
            Allowance = request.Allowance,
            Deduction = request.Deduction,
            NetPay = netPay,
            Status = PayrollStatus.Draft,
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _payrollRepository.AddAsync(payroll, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("employeeId", "员工", null, $"{employee.Name}（{employee.EmployeeNo}）")
                .Add("period", "期间", null, PayrollPeriod.Text(payroll.Year, payroll.Month))
                .Add("baseSalary", "基本工资", null, AuditSummary.Money(payroll.BaseSalary))
                .Add("allowance", "津贴", null, AuditSummary.Money(payroll.Allowance))
                .Add("deduction", "扣款", null, AuditSummary.Money(payroll.Deduction))
                .Add("netPay", "实发", null, AuditSummary.Money(payroll.NetPay))
                .Add("status", "状态", null, AuditText.PayrollStatus(payroll.Status))
                .Add("remark", "备注", null, payroll.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Payroll,
                Action = AuditAction.Create,
                ResourceId = payroll.Id,
                ResourceNo = employee.EmployeeNo,
                Summary = $"新增工资单 {employee.Name}（{PayrollPeriod.Text(payroll.Year, payroll.Month)}，实发 {AuditSummary.Money(payroll.NetPay)}）",
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
