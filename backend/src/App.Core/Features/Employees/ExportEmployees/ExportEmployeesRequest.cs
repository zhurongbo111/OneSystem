using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Exports;

namespace App.Core.Features.Employees.ExportEmployees;

/// <summary>
/// 员工列表导出请求（erp-export）：筛选参数与列表查询一致；
/// 分页参数仅为与列表请求同形，导出取「当前筛选全量」，不参与分页（specs/027-erp-export/design.md §0.2）
/// </summary>
public sealed class ExportEmployeesRequest : IRequest<ExportResultDto>
{
    /// <summary>页码，从 1 起（导出忽略，仅为与列表参数一致）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（导出忽略，仅为与列表参数一致）</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>关键词（工号 / 姓名模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>部门筛选，可空</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>岗位筛选，可空</summary>
    public Guid? PositionId { get; init; }

    /// <summary>在职状态筛选（1 在职 / 0 离职），可空</summary>
    public EmployeeStatus? Status { get; init; }
}
