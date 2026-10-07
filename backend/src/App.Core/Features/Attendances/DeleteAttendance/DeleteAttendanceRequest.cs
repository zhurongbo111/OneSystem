using App.Core.Abstractions;

namespace App.Core.Features.Attendances.DeleteAttendance;

/// <summary>
/// 删除考勤登记请求（考勤登记无状态，可删除）
/// </summary>
public sealed class DeleteAttendanceRequest : IRequest<object?>
{
    /// <summary>考勤记录 id（由路由提供）</summary>
    public Guid Id { get; init; }
}
