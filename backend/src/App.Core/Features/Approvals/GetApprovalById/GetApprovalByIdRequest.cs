using App.Core.Abstractions;

namespace App.Core.Features.Approvals.GetApprovalById;

/// <summary>
/// 审批详情查询请求（路由 id 绑定；无格式校验器，见 design.md §3.4）
/// </summary>
public sealed class GetApprovalByIdRequest : IRequest<ApprovalDetailDto>
{
    /// <summary>审批记录 id</summary>
    public Guid Id { get; init; }
}
