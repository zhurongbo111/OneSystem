using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Leads;
using App.Core.Features.Leads.ConvertLead;
using App.Core.Features.Leads.CreateLead;
using App.Core.Features.Leads.GetLeadById;
using App.Core.Features.Leads.GetLeads;
using App.Core.Features.Leads.UpdateLead;
using App.Core.Features.Leads.UpdateLeadStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 线索控制器（/api/leads）：列表分页 / 详情 / 新增 / 编辑 / 状态流转 / 转商机。
/// 线索是**售前意向数据**：不锁库存、不写库存流水、不产生应收（specs/043-erp-crm-presale design.md §1）；
/// 跟进活动见 <see cref="ActivitiesController"/>（/api/leads/{id}/activities）。
/// 统一 ApiResponse 包装，动作全部标注权限点（`028` 默认拒绝）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/leads")]
public sealed class LeadsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化线索控制器
    /// </summary>
    public LeadsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询线索（关键词 / 来源 / 状态 / 负责人筛选）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<LeadListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.LeadsView)]
    public async Task<ApiResponse<PagedResult<LeadListItemDto>>> GetPaged([FromQuery] GetLeadsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询线索详情（含负责人姓名与转出商机信息）
    /// </summary>
    /// <param name="id">线索 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<LeadDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.LeadsView)]
    public async Task<ApiResponse<LeadDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetLeadByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增线索（售前意向：不动库存、不写流水、不产生应收）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<LeadDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.LeadsCreate)]
    public async Task<ApiResponse<LeadDetailDto>> Create([FromBody] CreateLeadRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑线索（**全量覆盖**语义；终态不可改状态，状态与 <c>/status</c> 共用判据，40168）
    /// </summary>
    /// <param name="id">线索 id</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<LeadDetailDto>))]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.LeadsUpdate)]
    public async Task<ApiResponse<LeadDetailDto>> Update(Guid id, [FromBody] UpdateLeadRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateLeadRequest
        {
            Id = id,
            Name = request.Name,
            Contact = request.Contact,
            Phone = request.Phone,
            Source = request.Source,
            Status = request.Status,
            OwnerId = request.OwnerId,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 线索状态流转（终态不可再改；「已转化」只能由转商机产生，40168）
    /// </summary>
    /// <param name="id">线索 id</param>
    /// <param name="request">状态流转请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<LeadDetailDto>))]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.LeadsStatus)]
    public async Task<ApiResponse<LeadDetailDto>> UpdateStatus(Guid id, [FromBody] UpdateLeadStatusRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateLeadStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 线索转商机（一次性整转；终态线索不可转，40168）
    /// </summary>
    /// <param name="id">线索 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ConvertLeadResultDto>))]
    [HttpPost("{id:guid}/convert")]
    [RequirePermission(Permissions.LeadsConvert)]
    public async Task<ApiResponse<ConvertLeadResultDto>> Convert(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new ConvertLeadRequest { Id = id }, cancellationToken));
}
