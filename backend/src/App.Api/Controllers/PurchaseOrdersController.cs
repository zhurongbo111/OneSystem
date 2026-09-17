using App.Core.Abstractions;
using App.Core.Features.PurchaseOrders;
using App.Core.Features.PurchaseOrders.ClosePurchaseOrder;
using App.Core.Features.PurchaseOrders.CreatePurchaseOrder;
using App.Core.Features.PurchaseOrders.GetPurchaseOrderById;
using App.Core.Features.PurchaseOrders.GetPurchaseOrders;
using App.Core.Features.PurchaseOrders.UpdatePurchaseOrder;
using App.Core.Features.PurchaseOrders.VoidPurchaseOrder;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 采购订单控制器（/api/purchase-orders）：列表分页 / 详情 / 新增 / 编辑（仅待收货）/ 作废 / 关闭。
/// 订单是计划单据：不动库存、不写库存流水（specs/024-erp-order-flow design.md §1）；
/// 收货由采购入库单（/api/purchase-receipts）关联订单完成。
/// 统一 ApiResponse 包装，写接口强制鉴权。
/// </summary>
[Authorize]
[ApiController]
[Route("api/purchase-orders")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化采购订单控制器
    /// </summary>
    public PurchaseOrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询采购订单（含作废订单，作废 / 已关闭行前端置灰；含未收数量）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<PurchaseOrderListItemDto>>))]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<PurchaseOrderListItemDto>>> GetPaged([FromQuery] GetPurchaseOrdersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询采购订单详情（含明细的订购 / 已收 / 未收数量）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPurchaseOrderByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增采购订单（计划单据：不动库存、不写流水）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [HttpPost]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑采购订单（仅「待收货」状态允许；明细整体替换）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [HttpPut("{id:guid}")]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> Update(Guid id, [FromBody] UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdatePurchaseOrderRequest
        {
            Id = id,
            PartnerId = request.PartnerId,
            OrderDate = request.OrderDate,
            ExpectedDate = request.ExpectedDate,
            Items = request.Items,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 作废采购订单（仅「待收货」状态允许；仅改状态不删数据）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [HttpPut("{id:guid}/void")]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidPurchaseOrderRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 关闭采购订单（待收货 / 部分收货允许；剩余不再收货）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderDetailDto>))]
    [HttpPut("{id:guid}/close")]
    public async Task<ApiResponse<PurchaseOrderDetailDto>> Close(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new ClosePurchaseOrderRequest { Id = id }, cancellationToken));
}
