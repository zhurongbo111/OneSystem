using App.Core.Entities;

namespace App.Core.Features.Attendances;

/// <summary>
/// 考勤出参映射（集中一处，避免各用例重复拼装）。
/// 派生字段：<c>typeText</c>（请假 / 加班）与 <c>days</c>（含首尾的天数）随映射一并计算。
/// </summary>
internal static class AttendanceDtoMapper
{
    /// <summary>考勤实体 → 列表项出参</summary>
    public static AttendanceListItemDto ToAttendanceListItemDto(Attendance attendance)
        => new()
        {
            Id = attendance.Id.ToString(),
            EmployeeId = attendance.EmployeeId.ToString(),
            EmployeeName = attendance.EmployeeName,
            Type = (int)attendance.Type,
            TypeText = TypeText(attendance.Type),
            StartDate = attendance.StartDate,
            EndDate = attendance.EndDate,
            Days = Days(attendance.StartDate, attendance.EndDate),
            Remark = attendance.Remark,
            CreatedAt = attendance.CreatedAt,
        };

    /// <summary>考勤实体 → 详情出参</summary>
    public static AttendanceDetailDto ToAttendanceDetailDto(Attendance attendance)
        => new()
        {
            Id = attendance.Id.ToString(),
            EmployeeId = attendance.EmployeeId.ToString(),
            EmployeeName = attendance.EmployeeName,
            Type = (int)attendance.Type,
            TypeText = TypeText(attendance.Type),
            StartDate = attendance.StartDate,
            EndDate = attendance.EndDate,
            Days = Days(attendance.StartDate, attendance.EndDate),
            Remark = attendance.Remark,
            CreatedAt = attendance.CreatedAt,
            UpdatedAt = attendance.UpdatedAt,
        };

    /// <summary>考勤类型文案</summary>
    private static string TypeText(AttendanceType type)
        => type == AttendanceType.Leave ? "请假" : "加班";

    /// <summary>天数（含首尾：同日 = 1 天）</summary>
    private static int Days(DateOnly startDate, DateOnly endDate)
        => endDate.DayNumber - startDate.DayNumber + 1;
}
