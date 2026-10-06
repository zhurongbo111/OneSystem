using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Opportunities;
using App.Core.Features.Opportunities.CreateOpportunity;
using App.Core.Features.Opportunities.GetOpportunities;
using App.Core.Features.Opportunities.GetOpportunityById;
using App.Core.Features.Opportunities.UpdateOpportunity;
using App.Core.Features.Opportunities.UpdateOpportunityStage;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 商机控制器（/api/opportunities）：列表分页 / 详情 / 新增 / 编辑 / 阶段推进。
/// 商机是**售前意向数据**：不锁库存、不写库存流水、不产生应收（specs/043-erp-crm-presale design.md §1）；
/// 跟进活动见 <see cref="ActivitiesController"/>（/api/opportunities/{id}/activities）。
/// 统一 ApiResponse 包装，动作全部标注权限点（`028` 默认拒绝）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/opportunities")]
public sealed class OpportunitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化商机控制器
    /// </summary>
    public OpportunitiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询商机（关键词 / 阶段 / 客户 / 负责人筛选）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<OpportunityListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.OpportunitiesView)]
    public async Task<ApiResponse<PagedResult<OpportunityListItemDto>>> GetPaged([FromQuery] GetOpportunitiesRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询商机详情（含负责人姓名）
    /// </summary>
    /// <param name="id">商机 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<OpportunityDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.OpportunitiesView)]
    public async Task<ApiResponse<OpportunityDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetOpportunityByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增商机（售前意向：不动库存、不写流水、不产生应收；客户可空）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<OpportunityDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.OpportunitiesCreate)]
    public async Task<ApiResponse<OpportunityDetailDto>> Create([FromBody] CreateOpportunityRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑商机（**全量覆盖**语义；单号与来源线索不可改；终态不可改阶段，40169）
    /// </summary>
    /// <param name="id">商机 id</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<OpportunityDetailDto>))]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.OpportunitiesUpdate)]
    public async Task<ApiResponse<OpportunityDetailDto>> Update(Guid id, [FromBody] UpdateOpportunityRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateOpportunityRequest
        {
            Id = id,
            Name = request.Name,
            PartnerId = request.PartnerId,
            Amount = request.Amount,
            Stage = request.Stage,
            ExpectedCloseDate = request.ExpectedCloseDate,
            OwnerId = request.OwnerId,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 商机阶段推进（终态赢单 / 输单后不可再改，40169）
    /// </summary>
    /// <param name="id">商机 id</param>
    /// <param name="request">阶段推进请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<OpportunityDetailDto>))]
    [HttpPut("{id:guid}/stage")]
    [RequirePermission(Permissions.OpportunitiesStage)]
    public async Task<ApiResponse<OpportunityDetailDto>> UpdateStage(Guid id, [FromBody] UpdateOpportunityStageRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateOpportunityStageRequest { Id = id, Stage = request.Stage };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
