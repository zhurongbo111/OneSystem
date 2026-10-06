using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Approvals;
using App.Core.Features.Approvals.ApproveOrder;
using App.Core.Features.Approvals.GetApprovalById;
using App.Core.Features.Approvals.GetApprovals;
using App.Core.Features.Approvals.GetApprovalRules;
using App.Core.Features.Approvals.RejectApproval;
using App.Core.Features.Approvals.UpdateApprovalRules;
using App.Core.Features.Approvals.WithdrawApproval;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 单据审批控制器（specs/042-erp-approval/design.md §3.4）：
/// <c>/api/approvals</c>（列表 / 详情 / 通过 / 驳回 / 撤回）+ <c>/api/approval-rules</c>（规则查询 / 保存）。
/// 待审批单据在审批通过前**不产生任何库存 / 流水 / 成本变化**；规则未启用时四类单据行为与改造前逐条一致。
/// 统一 ApiResponse 包装，写接口强制鉴权。
/// </summary>
[Authorize]
[ApiController]
[Route("api/approvals")]
public sealed class ApprovalsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化单据审批控制器
    /// </summary>
    public ApprovalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询审批记录（「待我审批」= status 传 1；支持类型 / 提交人 / 时间筛选）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<ApprovalListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.ApprovalsView)]
    public async Task<ApiResponse<PagedResult<ApprovalListItemDto>>> GetPaged(
        [FromQuery] GetApprovalsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询审批详情（审批记录快照 + 被审批单据摘要与明细）
    /// </summary>
    /// <param name="id">审批记录 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ApprovalDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.ApprovalsView)]
    public async Task<ApiResponse<ApprovalDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetApprovalByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 审批通过（通过时执行单据生效；生效失败如库存不足则整体回滚、保持待审批）
    /// </summary>
    /// <param name="id">审批记录 id</param>
    /// <param name="request">审批请求（审批意见可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ApprovalDetailDto>))]
    [HttpPut("{id:guid}/approve")]
    [RequirePermission(Permissions.ApprovalsApprove)]
    public async Task<ApiResponse<ApprovalDetailDto>> Approve(
        Guid id, [FromBody] ApproveOrderRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new ApproveOrderRequest { Id = id, Remark = request.Remark };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 审批驳回（意见必填；驳回即单据作废，不做任何库存回冲）
    /// </summary>
    /// <param name="id">审批记录 id</param>
    /// <param name="request">驳回请求（审批意见必填）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ApprovalDetailDto>))]
    [HttpPut("{id:guid}/reject")]
    [RequirePermission(Permissions.ApprovalsApprove)]
    public async Task<ApiResponse<ApprovalDetailDto>> Reject(
        Guid id, [FromBody] RejectApprovalRequest request, CancellationToken cancellationToken)
    {
        var command = new RejectApprovalRequest { Id = id, Remark = request.Remark };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 撤回待审批单据（仅提交人本人；撤回即单据作废，不做任何库存回冲）
    /// </summary>
    /// <param name="id">审批记录 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ApprovalDetailDto>))]
    [HttpPut("{id:guid}/withdraw")]
    [RequirePermission(Permissions.ApprovalsView)]
    public async Task<ApiResponse<ApprovalDetailDto>> Withdraw(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new WithdrawApprovalRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 查询审批规则（四类单据各一行，未配置返回「未启用」）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<ApprovalRuleDto>>))]
    [HttpGet("/api/approval-rules")]
    [RequirePermission(Permissions.ApprovalsRules)]
    public async Task<ApiResponse<IReadOnlyList<ApprovalRuleDto>>> GetRules(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetApprovalRulesRequest(), cancellationToken));

    /// <summary>
    /// 保存审批规则（逐类型 upsert；阈值以上需审批，未启用则该类单据保存即生效）
    /// </summary>
    /// <param name="request">保存请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<ApprovalRuleDto>>))]
    [HttpPut("/api/approval-rules")]
    [RequirePermission(Permissions.ApprovalsRules)]
    public async Task<ApiResponse<IReadOnlyList<ApprovalRuleDto>>> UpdateRules(
        [FromBody] UpdateApprovalRulesRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
