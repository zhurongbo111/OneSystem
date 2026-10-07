namespace App.Core.Features.Attendances;

/// <summary>
/// 考勤记录列表项出参（考勤列表页）。
/// 枚举以**整型**输出（AttendanceType：0 请假 / 1 加班）；<see cref="TypeText"/> 为派生文案；
/// <see cref="Days"/> 为派生字段（含首尾），计算见 <c>AttendanceDtoMapper</c>。
/// </summary>
public sealed class AttendanceListItemDto
{
    /// <summary>考勤记录 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>员工 ID</summary>
    public string EmployeeId { get; init; } = string.Empty;

    /// <summary>员工姓名（快照）</summary>
    public string EmployeeName { get; init; } = string.Empty;

    /// <summary>考勤类型（0 请假 / 1 加班）</summary>
    public int Type { get; init; }

    /// <summary>考勤类型文案（请假 / 加班）</summary>
    public string TypeText { get; init; } = string.Empty;

    /// <summary>起始日</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>结束日</summary>
    public DateOnly EndDate { get; init; }

    /// <summary>天数（含首尾，派生字段）</summary>
    public int Days { get; init; }

    /// <summary>事由</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
