using App.Core.Abstractions;

namespace App.Core.Features.Approvals.GetApprovalRules;

/// <summary>
/// 审批规则查询请求（无入参；四类单据各返回一行，缺失的类型返回默认「未启用」，specs/042-erp-approval/design.md §3.4）
/// </summary>
public sealed class GetApprovalRulesRequest : IRequest<IReadOnlyList<ApprovalRuleDto>>
{
}
