using App.Core.Abstractions;

namespace App.Core.Features.Approvals.RejectApproval;

/// <summary>
/// 审批驳回请求（specs/042-erp-approval/design.md §3.4）：驳回即单据作废（从未生效，无库存回冲）
/// </summary>
public sealed class RejectApprovalRequest : IRequest<ApprovalDetailDto>
{
    /// <summary>审批记录 id（由 Controller 以路由值覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>审批意见（驳回必填，≤ 200 字符）</summary>
    public string? Remark { get; init; }
}
