using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Activities;
using App.Core.Features.Activities.CreateLeadActivity;
using App.Core.Features.Activities.CreateOpportunityActivity;
using App.Core.Features.Activities.GetLeadActivities;
using App.Core.Features.Activities.GetOpportunityActivities;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 跟进活动控制器（specs/043-erp-crm-presale design.md §3.3）：
/// 按归属域拆分端点（<c>/api/leads/{id}/activities</c> 与 <c>/api/opportunities/{id}/activities</c>），
/// 使权限点与主资源域一致、避免动态权限歧义（`leads.*` 与 `opportunities.*`，`028` §0.2）。
/// 活动**只增不改不删**（不做编辑 / 删除端点）：跟进留痕。
/// </summary>
[Authorize]
[ApiController]
[Route("api")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化跟进活动控制器
    /// </summary>
    public ActivitiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询线索跟进活动（跟进时间倒序）
    /// </summary>
    /// <param name="id">线索 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<ActivityItemDto>>))]
    [HttpGet("leads/{id:guid}/activities")]
    [RequirePermission(Permissions.LeadsView)]
    public async Task<ApiResponse<IReadOnlyList<ActivityItemDto>>> GetLeadActivities(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetLeadActivitiesRequest { LeadId = id }, cancellationToken));

    /// <summary>
    /// 新增线索跟进活动（只增；线索不存在报 40400）
    /// </summary>
    /// <param name="id">线索 id</param>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ActivityItemDto>))]
    [HttpPost("leads/{id:guid}/activities")]
    [RequirePermission(Permissions.LeadsUpdate)]
    public async Task<ApiResponse<ActivityItemDto>> CreateLeadActivity(Guid id, [FromBody] CreateLeadActivityRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new CreateLeadActivityRequest
        {
            LeadId = id,
            Type = request.Type,
            Content = request.Content,
            ActivityTime = request.ActivityTime,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 查询商机跟进活动（跟进时间倒序）
    /// </summary>
    /// <param name="id">商机 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<ActivityItemDto>>))]
    [HttpGet("opportunities/{id:guid}/activities")]
    [RequirePermission(Permissions.OpportunitiesView)]
    public async Task<ApiResponse<IReadOnlyList<ActivityItemDto>>> GetOpportunityActivities(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetOpportunityActivitiesRequest { OpportunityId = id }, cancellationToken));

    /// <summary>
    /// 新增商机跟进活动（只增；商机不存在报 40400）
    /// </summary>
    /// <param name="id">商机 id</param>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ActivityItemDto>))]
    [HttpPost("opportunities/{id:guid}/activities")]
    [RequirePermission(Permissions.OpportunitiesUpdate)]
    public async Task<ApiResponse<ActivityItemDto>> CreateOpportunityActivity(Guid id, [FromBody] CreateOpportunityActivityRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new CreateOpportunityActivityRequest
        {
            OpportunityId = id,
            Type = request.Type,
            Content = request.Content,
            ActivityTime = request.ActivityTime,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
