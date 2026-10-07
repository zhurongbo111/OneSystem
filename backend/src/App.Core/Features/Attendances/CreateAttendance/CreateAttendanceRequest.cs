using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Attendances.CreateAttendance;

/// <summary>
/// 新增考勤登记请求（请假 / 加班；员工姓名为后端取实体写入的快照）
/// </summary>
public sealed class CreateAttendanceRequest : IRequest<AttendanceDetailDto>
{
    /// <summary>员工 id（必填；须存在且在职）</summary>
    public Guid EmployeeId { get; init; }

    /// <summary>考勤类型（0 请假 / 1 加班）</summary>
    public int Type { get; init; } = (int)AttendanceType.Leave;

    /// <summary>起始日</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>结束日（不得早于起始日）</summary>
    public DateOnly EndDate { get; init; }

    /// <summary>事由，可空</summary>
    public string? Remark { get; init; }
}
