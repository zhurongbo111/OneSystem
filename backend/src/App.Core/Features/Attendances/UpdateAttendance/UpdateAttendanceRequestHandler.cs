using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Attendances.UpdateAttendance;

/// <summary>
/// 编辑考勤登记用例：记录存在 → 员工存在且在职 → 同类型区间不重叠（排除自身）→ 更新（与审计日志同事务）
/// </summary>
public sealed class UpdateAttendanceRequestHandler : IRequestHandler<UpdateAttendanceRequest, AttendanceDetailDto>
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑考勤登记用例处理器
    /// </summary>
    public UpdateAttendanceRequestHandler(
        IAttendanceRepository attendanceRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _attendanceRepository = attendanceRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑考勤登记请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<AttendanceDetailDto> HandleAsync(UpdateAttendanceRequest request, CancellationToken cancellationToken = default)
    {
        var attendance = await _attendanceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "考勤记录不存在");

        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "员工不存在");

        // 查库约束：离职员工不可登记考勤（含改挂到离职员工）
        if (employee.Status != EmployeeStatus.Active)
        {
            throw new BusinessException(ErrorCode.Validation, "该员工已离职，不可登记考勤");
        }

        var type = (AttendanceType)request.Type;

        // 查库约束：同类型区间不得重叠（排除自身，message 含冲突区间）
        var overlap = await _attendanceRepository.FindOverlapAsync(
            employee.Id, type, request.StartDate, request.EndDate, attendance.Id, cancellationToken);
        if (overlap is not null)
        {
            throw new BusinessException(
                ErrorCode.Validation,
                $"该员工 {overlap.StartDate:yyyy-MM-dd} ~ {overlap.EndDate:yyyy-MM-dd} 已有{AuditText.AttendanceType(type)}记录，日期不可重叠");
        }

        var beforeType = attendance.Type;
        var beforeStartDate = attendance.StartDate;
        var beforeEndDate = attendance.EndDate;
        var beforeRemark = attendance.Remark;
        var beforeEmployeeId = attendance.EmployeeId;

        var now = DateTimeOffset.UtcNow;
        attendance.EmployeeId = employee.Id;
        attendance.EmployeeName = employee.Name;
        attendance.Type = type;
        attendance.StartDate = request.StartDate;
        attendance.EndDate = request.EndDate;
        attendance.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        attendance.UpdatedAt = now;
        attendance.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _attendanceRepository.UpdateAsync(attendance, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add(
                    "employeeId",
                    "员工",
                    beforeEmployeeId == employee.Id ? null : beforeEmployeeId.ToString(),
                    beforeEmployeeId == employee.Id ? null : $"{employee.Name}（{employee.EmployeeNo}）")
                .Add("type", "类型", AuditText.AttendanceType(beforeType), AuditText.AttendanceType(attendance.Type))
                .Add("startDate", "起始日", AuditSummary.Date(beforeStartDate), AuditSummary.Date(attendance.StartDate))
                .Add("endDate", "结束日", AuditSummary.Date(beforeEndDate), AuditSummary.Date(attendance.EndDate))
                .Add("remark", "事由", beforeRemark, attendance.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Attendance,
                Action = AuditAction.Update,
                ResourceId = attendance.Id,
                ResourceNo = employee.EmployeeNo,
                Summary = $"修改{AuditText.AttendanceType(attendance.Type)} {employee.Name}（{AuditSummary.Date(attendance.StartDate)} ~ {AuditSummary.Date(attendance.EndDate)}）",
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

        return AttendanceDtoMapper.ToAttendanceDetailDto(attendance);
    }
}
