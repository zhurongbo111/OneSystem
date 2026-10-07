using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Attendances.UpdateAttendance;

/// <summary>
/// 编辑考勤登记请求（全量覆盖，AGENTS.md §4.5；员工可改，改后姓名快照随之刷新）
/// </summary>
public sealed class UpdateAttendanceRequest : IRequest<AttendanceDetailDto>
{
    /// <summary>考勤记录 id（由路由提供，请求体可缺省）</summary>
    public Guid Id { get; init; }

    /// <summary>员工 id（必填；须存在且在职）</summary>
    public Guid EmployeeId { get; init; }

    /// <summary>考勤类型（0 请假 / 1 加班）</summary>
    public int Type { get; init; } = (int)AttendanceType.Leave;

    /// <summary>起始日</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>结束日（不得早于起始日）</summary>
    public DateOnly EndDate { get; init; }

    /// <summary>事由，可空（缺省 / 空白视为清空）</summary>
    public string? Remark { get; init; }
}
