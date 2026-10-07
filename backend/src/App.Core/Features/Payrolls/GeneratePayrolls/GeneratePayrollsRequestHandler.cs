using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;

namespace App.Core.Features.Payrolls.GeneratePayrolls;

/// <summary>
/// 批量生成工资单用例：取该期间**在职**员工 → 逐人跳过已存在 → 批量新增草稿（与审计日志同事务）。
/// 幂等：可重复执行，重复的员工计入 <c>skipped</c>，不产生重复工资单。
/// 草稿金额一律为 0（基本工资由人工按实际填写，本期不做薪资结构）。
/// </summary>
public sealed class GeneratePayrollsRequestHandler : IRequestHandler<GeneratePayrollsRequest, GeneratePayrollsResponse>
{
    private readonly IPayrollRepository _payrollRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化批量生成工资单用例处理器
    /// </summary>
    public GeneratePayrollsRequestHandler(
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
    /// 处理批量生成工资单请求
    /// </summary>
    /// <param name="request">批量生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<GeneratePayrollsResponse> HandleAsync(GeneratePayrollsRequest request, CancellationToken cancellationToken = default)
    {
        var employees = await _payrollRepository.GetEmployeesForPeriodAsync(request.Year, request.Month, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var created = new List<Payroll>();
        var skipped = 0;

        foreach (var employee in employees)
        {
            if (await _payrollRepository.ExistsAsync(employee.Id, request.Year, request.Month, cancellationToken))
            {
                skipped++;
                continue;
            }

            created.Add(new Payroll
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                Year = request.Year,
                Month = request.Month,
                BaseSalary = 0m,
                Allowance = 0m,
                Deduction = 0m,
                NetPay = PayrollAmountCalculator.NetPay(0m, 0m, 0m),
                Status = PayrollStatus.Draft,
                Remark = null,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            });
        }

        // 批量写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _payrollRepository.AddRangeAsync(created, cancellationToken);

            var period = PayrollPeriod.Text(request.Year, request.Month);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Payroll,
                Action = AuditAction.Create,
                ResourceId = null,
                ResourceNo = period,
                Summary = $"批量生成 {period} 工资单：新增 {created.Count} 条、跳过 {skipped} 条",
                Changes = null,
                ChangesTruncated = false,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return new GeneratePayrollsResponse
        {
            Created = created.Count,
            Skipped = skipped,
        };
    }
}
