using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Attendances.GetAttendances;

/// <summary>
/// 考勤记录分页列表请求（Query 参数绑定）
/// </summary>
public sealed class GetAttendancesRequest : IRequest<PagedResult<AttendanceListItemDto>>
{
    /// <summary>员工筛选，可空</summary>
    public Guid? EmployeeId { get; init; }

    /// <summary>考勤类型筛选（0 请假 / 1 加班），可空表示全部</summary>
    public int? Type { get; init; }

    /// <summary>起始日（区间重叠筛选），可空</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>结束日（区间重叠筛选），可空</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
