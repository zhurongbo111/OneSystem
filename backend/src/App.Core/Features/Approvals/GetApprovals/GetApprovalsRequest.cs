using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Approvals.GetApprovals;

/// <summary>
/// 审批记录分页查询请求（specs/042-erp-approval/design.md §3.4）；
/// 「待我审批」= <see cref="Status"/> 传 <see cref="ApprovalStatus.Pending"/>
/// </summary>
public sealed class GetApprovalsRequest : IRequest<PagedResult<ApprovalListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>审批状态，可空（不传 = 全部）</summary>
    public ApprovalStatus? Status { get; init; }

    /// <summary>单据类型，可空</summary>
    public SettlementOrderType? OrderType { get; init; }

    /// <summary>提交人 id，可空</summary>
    public Guid? SubmittedBy { get; init; }

    /// <summary>起始提交时间（含），可空</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束提交时间（含），可空</summary>
    public DateTimeOffset? End { get; init; }
}
