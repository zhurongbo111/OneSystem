using App.Core.Abstractions;
using App.Core.Features.Partners;
using App.Core.Features.Partners.CreatePartner;
using App.Core.Features.Partners.GetPartnerById;
using App.Core.Features.Partners.GetPartners;
using App.Core.Features.Partners.UpdatePartner;
using App.Core.Features.Partners.UpdatePartnerStatus;
using App.Core.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 往来单位接口（需登录）：供应商 / 客户合并一张表，开单单位选择数据源
/// </summary>
[Authorize]
[ApiController]
[Route("api/partners")]
public class PartnersController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化往来单位控制器
    /// </summary>
    public PartnersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询往来单位（关键词 / 类型 / 状态筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<PartnerDto>>))]
    [ProducesResponseType(401)]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<PartnerDto>>> GetPartners([FromQuery] GetPartnersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增往来单位
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PartnerDto>))]
    [ProducesResponseType(401)]
    [HttpPost]
    public async Task<ApiResponse<PartnerDto>> CreatePartner([FromBody] CreatePartnerRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询往来单位详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PartnerDto>))]
    [ProducesResponseType(401)]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<PartnerDto>> GetPartnerById([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPartnerByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑往来单位（名称创建后不可修改，请求体不含 name）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PartnerDto>))]
    [ProducesResponseType(401)]
    [HttpPut("{id:guid}")]
    public async Task<ApiResponse<PartnerDto>> UpdatePartner(
        [FromRoute] Guid id,
        [FromBody] UpdatePartnerRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdatePartnerRequest
        {
            Id = id,
            Type = request.Type,
            Contact = request.Contact,
            Phone = request.Phone,
            Address = request.Address,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 停用 / 启用往来单位（停用后不可被新单据选择，保留历史数据）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PartnerDto>))]
    [ProducesResponseType(401)]
    [HttpPut("{id:guid}/status")]
    public async Task<ApiResponse<PartnerDto>> UpdatePartnerStatus(
        [FromRoute] Guid id,
        [FromBody] UpdatePartnerStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePartnerStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
