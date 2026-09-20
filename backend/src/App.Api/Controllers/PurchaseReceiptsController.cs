using App.Api.Http;
using App.Core.Abstractions;
using App.Core.Features.PurchaseReceipts;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReceipts.ExportPurchaseReceipts;
using App.Core.Features.PurchaseReceipts.GetPurchaseOrderLines;
using App.Core.Features.PurchaseReceipts.GetPurchaseOrderPicks;
using App.Core.Features.PurchaseReceipts.GetPurchaseReceiptById;
using App.Core.Features.PurchaseReceipts.GetPurchaseReceipts;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 采购单控制器（/api/purchase-receipts）：列表分页 / 详情 / 新增（保存即生效）/ 作废回冲。
/// 结算金额由收付款单核销驱动（POST /api/settlements），本控制器不再提供手工结算切换（specs/023-erp-settlement）。
/// 只读接口匿名放行、写接口强制鉴权（design.md §3.3）；统一 ApiResponse 包装。
/// </summary>
[Authorize]
[ApiController]
[Route("api/purchase-receipts")]
public sealed class PurchaseReceiptsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化采购单控制器
    /// </summary>
    public PurchaseReceiptsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询采购单（含作废单据，作废行前端置灰）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<PurchaseReceiptListItemDto>>))]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<PurchaseReceiptListItemDto>>> GetPaged([FromQuery] GetPurchaseReceiptsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 可关联采购订单候选（指定供应商，状态为待收货 / 部分收货；入库开单页「关联订单」下拉）
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<PurchaseOrderPickDto>>))]
    [HttpGet("pick-orders")]
    public async Task<ApiResponse<IReadOnlyList<PurchaseOrderPickDto>>> PickOrders([FromQuery] GetPurchaseOrderPicksRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 关联订单明细（含未收数量；入库开单页选择订单后带出明细与单价）
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseOrderLinesDto>))]
    [HttpGet("order-lines")]
    public async Task<ApiResponse<PurchaseOrderLinesDto>> OrderLines([FromQuery] GetPurchaseOrderLinesRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 导出采购入库单为 xlsx（erp-export）：沿用列表筛选，导出当前筛选全量（不受分页限制），
    /// 含「单据 + 明细」两个工作表。成功返回二进制文件流（契约例外，specs/027-erp-export/design.md §0.1）；
    /// 参数非法 / 服务端异常仍返回统一响应 JSON。固定段 export 置于 {id:guid} 之前注册
    /// </summary>
    /// <param name="request">导出请求（Query 绑定，筛选参数同列表）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ExportPurchaseReceiptsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 查询采购单详情（含明细行，快照字段原样返回）
    /// </summary>
    /// <param name="id">采购单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseReceiptDetailDto>))]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<PurchaseReceiptDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPurchaseReceiptByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增采购单（一步式：保存即生效，库存立即增加）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseReceiptDetailDto>))]
    [HttpPost]
    public async Task<ApiResponse<PurchaseReceiptDetailDto>> Create([FromBody] CreatePurchaseReceiptRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 作废采购单（回冲库存；仅改状态不删数据）
    /// </summary>
    /// <param name="id">采购单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseReceiptDetailDto>))]
    [HttpPut("{id:guid}/void")]
    public async Task<ApiResponse<PurchaseReceiptDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidPurchaseReceiptRequest { Id = id }, cancellationToken));

}
