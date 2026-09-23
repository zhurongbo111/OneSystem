using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Quotations;
using App.Core.Features.Quotations.ConvertToOrder;
using App.Core.Features.Quotations.CreateQuotation;
using App.Core.Features.Quotations.GetQuotationById;
using App.Core.Features.Quotations.GetQuotations;
using App.Core.Features.Quotations.UpdateQuotation;
using App.Core.Features.Quotations.VoidQuotation;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 报价单控制器（/api/quotations）：列表分页 / 详情 / 新增 / 编辑（仅草稿）/ 作废（仅草稿）/ 转销售订单（仅草稿）。
/// 报价单是**意向单据**：不锁库存、不写库存流水、不产生应收（specs/037-erp-quotation design.md §1）；
/// 成交由转单后的销售订单（/api/sales-orders）承担。
/// 统一 ApiResponse 包装，写接口强制鉴权。
/// </summary>
[Authorize]
[ApiController]
[Route("api/quotations")]
public sealed class QuotationsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化报价单控制器
    /// </summary>
    public QuotationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询报价单（含已转订单 / 已作废单，作废行前端置灰；含明细行数）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<QuotationListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.QuotationsView)]
    public async Task<ApiResponse<PagedResult<QuotationListItemDto>>> GetPaged([FromQuery] GetQuotationsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询报价单详情（含明细与转单信息）
    /// </summary>
    /// <param name="id">报价单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<QuotationDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.QuotationsView)]
    public async Task<ApiResponse<QuotationDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetQuotationByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增报价单（意向单据：不动库存、不写流水、不产生应收）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<QuotationDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.QuotationsCreate)]
    public async Task<ApiResponse<QuotationDetailDto>> Create([FromBody] CreateQuotationRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑报价单（仅「草稿」状态允许，否则 40166；明细整体替换）
    /// </summary>
    /// <param name="id">报价单 id</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<QuotationDetailDto>))]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.QuotationsUpdate)]
    public async Task<ApiResponse<QuotationDetailDto>> Update(Guid id, [FromBody] UpdateQuotationRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateQuotationRequest
        {
            Id = id,
            PartnerId = request.PartnerId,
            QuotationDate = request.QuotationDate,
            ValidUntil = request.ValidUntil,
            Items = request.Items,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 作废报价单（仅「草稿」状态允许，否则 40166；仅改状态不删数据）
    /// </summary>
    /// <param name="id">报价单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<QuotationDetailDto>))]
    [HttpPut("{id:guid}/void")]
    [RequirePermission(Permissions.QuotationsVoid)]
    public async Task<ApiResponse<QuotationDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidQuotationRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 报价单转销售订单（一次性整单转；仅「草稿」状态允许，否则 40167）
    /// </summary>
    /// <param name="id">报价单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ConvertQuotationResultDto>))]
    [HttpPost("{id:guid}/convert")]
    [RequirePermission(Permissions.QuotationsConvert)]
    public async Task<ApiResponse<ConvertQuotationResultDto>> Convert(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new ConvertQuotationRequest { Id = id }, cancellationToken));
}
