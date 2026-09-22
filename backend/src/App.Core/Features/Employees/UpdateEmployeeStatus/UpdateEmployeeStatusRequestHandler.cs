using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Employees.UpdateEmployeeStatus;

/// <summary>
/// 员工在职 / 离职切换用例：存在性 → 更新状态（离职且无日期时补当天；回到在职清空离职日期）
/// → 更新（与审计日志同事务）。离职仅改状态，不删除记录。
/// </summary>
public sealed class UpdateEmployeeStatusRequestHandler : IRequestHandler<UpdateEmployeeStatusRequest, EmployeeDetailDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化员工在职 / 离职切换用例处理器
    /// </summary>
    public UpdateEmployeeStatusRequestHandler(
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理员工在职 / 离职切换请求
    /// </summary>
    /// <param name="request">切换请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<EmployeeDetailDto> HandleAsync(UpdateEmployeeStatusRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "员工不存在");

        var beforeStatus = employee.Status;
        var beforeResignDate = employee.ResignDate;

        var now = DateTimeOffset.UtcNow;
        var status = (EmployeeStatus)request.Status;
        employee.Status = status;
        employee.ResignDate = EmployeeInputNormalizer.ResolveResignDate(status, employee.ResignDate, now);
        employee.UpdatedAt = now;
        employee.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _employeeRepository.UpdateAsync(employee, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.EmployeeStatus(beforeStatus), AuditText.EmployeeStatus(employee.Status))
                .Add("resignDate", "离职日期", AuditSummary.Date(beforeResignDate), AuditSummary.Date(employee.ResignDate));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Employee,
                Action = AuditAction.StatusChange,
                ResourceId = employee.Id,
                ResourceNo = employee.EmployeeNo,
                Summary = $"{(employee.Status == EmployeeStatus.Active ? "复职" : "办理离职")}员工 {employee.Name}（{employee.EmployeeNo}）",
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

        return await EmployeeDetailLoader.LoadAsync(_employeeRepository, employee.Id, cancellationToken);
    }
}
