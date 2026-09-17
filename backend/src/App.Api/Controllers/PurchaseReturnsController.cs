using App.Core.Abstractions;
using App.Core.Features.PurchaseReturns;
using App.Core.Features.PurchaseReturns.CreatePurchaseReturn;
using App.Core.Features.PurchaseReturns.GetPurchaseReturnById;
using App.Core.Features.PurchaseReturns.GetPurchaseReturns;
using App.Core.Features.PurchaseReturns.VoidPurchaseReturn;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 采购退货单控制器（/api/purchase-returns）：列表分页 / 详情 / 新增（保存即生效）/ 作废回冲。
/// 结算金额由收付款单核销驱动（POST /api/settlements），本控制器不再提供手工结算切换（specs/023-erp-settlement）。
/// 统一 ApiResponse 包装；鉴权同既有单据域（全局 [Authorize]，登录即可见，权限由 028-erp-rbac 接入）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/purchase-returns")]
public sealed class PurchaseReturnsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化采购退货单控制器
    /// </summary>
    public PurchaseReturnsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询采购退货单（含作废单据，作废行前端置灰）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<PurchaseReturnListItemDto>>))]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<PurchaseReturnListItemDto>>> GetPaged([FromQuery] GetPurchaseReturnsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询采购退货单详情（含明细行，快照字段原样返回）
    /// </summary>
    /// <param name="id">采购退货单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseReturnDetailDto>))]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<PurchaseReturnDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPurchaseReturnByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增采购退货单（一步式：保存即生效，库存立即减少并写流水）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseReturnDetailDto>))]
    [HttpPost]
    public async Task<ApiResponse<PurchaseReturnDetailDto>> Create([FromBody] CreatePurchaseReturnRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 作废采购退货单（回冲库存；仅改状态不删数据）
    /// </summary>
    /// <param name="id">采购退货单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PurchaseReturnDetailDto>))]
    [HttpPut("{id:guid}/void")]
    public async Task<ApiResponse<PurchaseReturnDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidPurchaseReturnRequest { Id = id }, cancellationToken));

}
