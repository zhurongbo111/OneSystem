using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.ServiceTickets;
using App.Core.Features.ServiceTickets.AssignServiceTicket;
using App.Core.Features.ServiceTickets.CreateServiceTicket;
using App.Core.Features.ServiceTickets.GetServiceTicketById;
using App.Core.Features.ServiceTickets.GetServiceTickets;
using App.Core.Features.ServiceTickets.UpdateServiceTicket;
using App.Core.Features.ServiceTickets.UpdateServiceTicketStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 服务工单控制器（/api/service-tickets）：列表分页 / 详情 / 登记 / 编辑 / 状态流转 / 指派负责人。
/// 工单是**售后留痕记录**：不做删除（关闭即归档），不产生库存 / 资金影响
/// （specs/045-erp-crm-service design.md §1）。
/// 统一 ApiResponse 包装，动作全部标注权限点（`028` 默认拒绝）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/service-tickets")]
public sealed class ServiceTicketsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化服务工单控制器
    /// </summary>
    public ServiceTicketsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询服务工单（关键词 / 状态 / 优先级 / 负责人筛选）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<ServiceTicketListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.ServiceTicketsView)]
    public async Task<ApiResponse<PagedResult<ServiceTicketListItemDto>>> GetPaged([FromQuery] GetServiceTicketsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询服务工单详情（含负责人姓名）
    /// </summary>
    /// <param name="id">工单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ServiceTicketDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.ServiceTicketsView)]
    public async Task<ApiResponse<ServiceTicketDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetServiceTicketByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 登记服务工单（售后留痕：不动库存、不写流水、不产生应收）
    /// </summary>
    /// <param name="request">登记请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ServiceTicketDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.ServiceTicketsCreate)]
    public async Task<ApiResponse<ServiceTicketDetailDto>> Create([FromBody] CreateServiceTicketRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑服务工单（**全量覆盖**语义；已关闭不可编辑，40172；状态需走 <c>/status</c>）
    /// </summary>
    /// <param name="id">工单 id</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ServiceTicketDetailDto>))]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.ServiceTicketsUpdate)]
    public async Task<ApiResponse<ServiceTicketDetailDto>> Update(Guid id, [FromBody] UpdateServiceTicketRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateServiceTicketRequest
        {
            Id = id,
            PartnerId = request.PartnerId,
            Contact = request.Contact,
            Phone = request.Phone,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            OwnerId = request.OwnerId,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 服务工单状态流转（白名单外 / 已关闭终态 → 40172；置「已解决」记解决时间，重开清空）
    /// </summary>
    /// <param name="id">工单 id</param>
    /// <param name="request">状态流转请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ServiceTicketDetailDto>))]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.ServiceTicketsStatus)]
    public async Task<ApiResponse<ServiceTicketDetailDto>> UpdateStatus(Guid id, [FromBody] UpdateServiceTicketStatusRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateServiceTicketStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 指派服务工单负责人（已关闭不可指派，40172；负责人必须存在，40400）
    /// </summary>
    /// <param name="id">工单 id</param>
    /// <param name="request">指派请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ServiceTicketDetailDto>))]
    [HttpPut("{id:guid}/assign")]
    [RequirePermission(Permissions.ServiceTicketsAssign)]
    public async Task<ApiResponse<ServiceTicketDetailDto>> Assign(Guid id, [FromBody] AssignServiceTicketRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new AssignServiceTicketRequest { Id = id, OwnerId = request.OwnerId };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
