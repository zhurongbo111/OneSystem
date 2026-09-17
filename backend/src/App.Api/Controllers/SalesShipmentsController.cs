using App.Core.Abstractions;
using App.Core.Features.SalesShipments;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.SalesShipments.GetSalesOrderLines;
using App.Core.Features.SalesShipments.GetSalesOrderPicks;
using App.Core.Features.SalesShipments.GetSalesShipmentById;
using App.Core.Features.SalesShipments.GetSalesShipments;
using App.Core.Features.SalesShipments.VoidSalesShipment;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 销售单控制器（/api/sales-shipments）：列表分页 / 详情 / 新增（保存即生效，库存减少）/ 作废回冲。
/// 结算金额由收付款单核销驱动（POST /api/settlements），本控制器不再提供手工结算切换（specs/023-erp-settlement）。
/// 写接口强制鉴权（design.md §3.3）；统一 ApiResponse 包装。
/// </summary>
[Authorize]
[ApiController]
[Route("api/sales-shipments")]
public sealed class SalesShipmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化销售单控制器
    /// </summary>
    public SalesShipmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询销售单（含作废单据，作废行前端置灰）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<SalesShipmentListItemDto>>))]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<SalesShipmentListItemDto>>> GetPaged([FromQuery] GetSalesShipmentsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 可关联销售订单候选（指定客户，状态为待发货 / 部分发货；出库开单页「关联订单」下拉）
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<SalesOrderPickDto>>))]
    [HttpGet("pick-orders")]
    public async Task<ApiResponse<IReadOnlyList<SalesOrderPickDto>>> PickOrders([FromQuery] GetSalesOrderPicksRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 关联订单明细（含未发数量；出库开单页选择订单后带出明细与单价）
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderLinesDto>))]
    [HttpGet("order-lines")]
    public async Task<ApiResponse<SalesOrderLinesDto>> OrderLines([FromQuery] GetSalesOrderLinesRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询销售单详情（含明细行，快照字段原样返回）
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesShipmentDetailDto>))]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<SalesShipmentDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetSalesShipmentByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增销售单（一步式：保存即生效，库存立即减少；任一行库存不足整单拒绝 40103）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesShipmentDetailDto>))]
    [HttpPost]
    public async Task<ApiResponse<SalesShipmentDetailDto>> Create([FromBody] CreateSalesShipmentRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 作废销售单（回冲库存；仅改状态不删数据）
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesShipmentDetailDto>))]
    [HttpPut("{id:guid}/void")]
    public async Task<ApiResponse<SalesShipmentDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidSalesShipmentRequest { Id = id }, cancellationToken));

}
