using App.Core.Abstractions;
using App.Core.Features.Purchases;
using App.Core.Features.Purchases.CreatePurchaseOrder;
using App.Core.Features.Purchases.GetPurchaseOrderById;
using App.Core.Features.Purchases.GetPurchaseOrders;
using App.Core.Features.Purchases.UpdatePurchaseOrderSettlement;
using App.Core.Features.Purchases.VoidPurchaseOrder;
using App.Core.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 采购单控制器（/api/purchase-orders）：列表分页 / 详情 / 新增（保存即生效）/ 作废回冲 / 结算切换。
/// 只读接口匿名放行、写接口强制鉴权（design.md §3.3）；统一 ApiResponse 包装。
/// </summary>
[Authorize]
[ApiController]
[Route("api/purchase-orders")]
public sealed class PurchaseOrderController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化采购单控制器
    /// </summary>
    public PurchaseOrderController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询采购单（含作废单据，作废行前端置灰）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<PurchaseOrderListItemDto>>))]
    [ProducesResponseType(401)]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<PurchaseOrderListItemDto>>> GetPaged([FromQuery] GetPurchaseOrdersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询采购单详情（含明细行，快照字段原样返回）
    /// </summary>
    /// <param name="id">采购单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [ProducesResponseType(401)]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPurchaseOrderByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增采购单（一步式：保存即生效，库存立即增加）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [ProducesResponseType(401)]
    [HttpPost]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 作废采购单（回冲库存；仅改状态不删数据）
    /// </summary>
    /// <param name="id">采购单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [ProducesResponseType(401)]
    [HttpPut("{id:guid}/void")]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidPurchaseOrderRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 更新结算状态（仅 未付 ↔ 已付；库存不变）
    /// </summary>
    /// <param name="id">采购单 id</param>
    /// <param name="request">结算更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [ProducesResponseType(401)]
    [HttpPut("{id:guid}/settlement")]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> UpdateSettlement(
        [FromRoute] Guid id,
        [FromBody] UpdatePurchaseOrderSettlementRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePurchaseOrderSettlementRequest { Id = id, SettlementStatus = request.SettlementStatus };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
