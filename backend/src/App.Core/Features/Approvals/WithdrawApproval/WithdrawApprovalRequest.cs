using App.Core.Abstractions;

namespace App.Core.Features.Approvals.WithdrawApproval;

/// <summary>
/// 撤回待审批单据请求（specs/042-erp-approval/design.md §3.4）：仅**提交人本人**可撤回，撤回即单据作废
/// </summary>
public sealed class WithdrawApprovalRequest : IRequest<ApprovalDetailDto>
{
    /// <summary>审批记录 id（由 Controller 以路由值覆盖）</summary>
    public Guid Id { get; init; }
}
