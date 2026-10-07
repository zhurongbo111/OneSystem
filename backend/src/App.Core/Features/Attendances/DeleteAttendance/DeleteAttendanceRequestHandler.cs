using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Attendances.DeleteAttendance;

/// <summary>
/// 删除考勤登记用例：记录存在 → 物理删除（与审计日志同事务）。
/// 考勤登记无状态与下游引用，删除无需额外保护。
/// </summary>
public sealed class DeleteAttendanceRequestHandler : IRequestHandler<DeleteAttendanceRequest, object?>
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除考勤登记用例处理器
    /// </summary>
    public DeleteAttendanceRequestHandler(
        IAttendanceRepository attendanceRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _attendanceRepository = attendanceRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除考勤登记请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeleteAttendanceRequest request, CancellationToken cancellationToken = default)
    {
        var attendance = await _attendanceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "考勤记录不存在");

        var now = DateTimeOffset.UtcNow;

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _attendanceRepository.DeleteAsync(attendance, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("employeeId", "员工", attendance.EmployeeName, null)
                .Add("type", "类型", AuditText.AttendanceType(attendance.Type), null)
                .Add("startDate", "起始日", AuditSummary.Date(attendance.StartDate), null)
                .Add("endDate", "结束日", AuditSummary.Date(attendance.EndDate), null)
                .Add("remark", "事由", attendance.Remark, null);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Attendance,
                Action = AuditAction.Delete,
                ResourceId = attendance.Id,
                ResourceNo = attendance.EmployeeName,
                Summary = $"删除{AuditText.AttendanceType(attendance.Type)} {attendance.EmployeeName}（{AuditSummary.Date(attendance.StartDate)} ~ {AuditSummary.Date(attendance.EndDate)}）",
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
