using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Attendances.CreateAttendance;

/// <summary>
/// 新增考勤登记用例：员工存在且**在职** → 同类型日期区间不重叠 → 落库（与审计日志同事务）
/// </summary>
public sealed class CreateAttendanceRequestHandler : IRequestHandler<CreateAttendanceRequest, AttendanceDetailDto>
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增考勤登记用例处理器
    /// </summary>
    public CreateAttendanceRequestHandler(
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
    /// 处理新增考勤登记请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<AttendanceDetailDto> HandleAsync(CreateAttendanceRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "员工不存在");

        // 查库约束：离职员工不可新登记考勤
        if (employee.Status != EmployeeStatus.Active)
        {
            throw new BusinessException(ErrorCode.Validation, "该员工已离职，不可登记考勤");
        }

        var type = (AttendanceType)request.Type;
        var remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();

        // 查库约束：同一员工同一类型的日期区间不得重叠（message 含冲突区间）
        var overlap = await _attendanceRepository.FindOverlapAsync(
            employee.Id, type, request.StartDate, request.EndDate, null, cancellationToken);
        if (overlap is not null)
        {
            throw new BusinessException(
                ErrorCode.Validation,
                $"该员工 {overlap.StartDate:yyyy-MM-dd} ~ {overlap.EndDate:yyyy-MM-dd} 已有{AuditText.AttendanceType(type)}记录，日期不可重叠");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var attendance = new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Remark = remark,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _attendanceRepository.AddAsync(attendance, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("employeeId", "员工", null, $"{employee.Name}（{employee.EmployeeNo}）")
                .Add("type", "类型", null, AuditText.AttendanceType(attendance.Type))
                .Add("startDate", "起始日", null, AuditSummary.Date(attendance.StartDate))
                .Add("endDate", "结束日", null, AuditSummary.Date(attendance.EndDate))
                .Add("remark", "事由", null, attendance.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Attendance,
                Action = AuditAction.Create,
                ResourceId = attendance.Id,
                ResourceNo = employee.EmployeeNo,
                Summary = $"登记{AuditText.AttendanceType(attendance.Type)} {employee.Name}（{AuditSummary.Date(attendance.StartDate)} ~ {AuditSummary.Date(attendance.EndDate)}）",
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
