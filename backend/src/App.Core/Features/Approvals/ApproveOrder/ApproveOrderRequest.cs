using App.Core.Abstractions;

namespace App.Core.Features.Approvals.ApproveOrder;

/// <summary>
/// 审批通过请求（specs/042-erp-approval/design.md §3.4）：通过即在同一事务内执行单据生效
/// </summary>
public sealed class ApproveOrderRequest : IRequest<ApprovalDetailDto>
{
    /// <summary>审批记录 id（由 Controller 以路由值覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>审批意见（可空，≤ 200 字符）</summary>
    public string? Remark { get; init; }
}
