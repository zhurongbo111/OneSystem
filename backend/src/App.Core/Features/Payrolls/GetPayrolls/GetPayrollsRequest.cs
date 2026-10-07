using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Payrolls.GetPayrolls;

/// <summary>
/// 工资单分页列表请求（Query 参数绑定）
/// </summary>
public sealed class GetPayrollsRequest : IRequest<PagedResult<PayrollListItemDto>>
{
    /// <summary>期间年筛选，可空</summary>
    public int? Year { get; init; }

    /// <summary>期间月筛选（1–12），可空</summary>
    public int? Month { get; init; }

    /// <summary>员工筛选，可空</summary>
    public Guid? EmployeeId { get; init; }

    /// <summary>状态筛选（0 草稿 / 1 已发放），可空表示全部</summary>
    public int? Status { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
