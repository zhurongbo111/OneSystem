using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Employees.GetEmployees;

/// <summary>
/// 员工分页列表请求（Query 参数绑定）
/// </summary>
public sealed class GetEmployeesRequest : IRequest<PagedResult<EmployeeListItemDto>>
{
    /// <summary>关键词（工号 / 姓名模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>部门筛选，可空</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>岗位筛选，可空</summary>
    public Guid? PositionId { get; init; }

    /// <summary>在职状态筛选（1 在职 / 0 离职），可空表示全部</summary>
    public int? Status { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
