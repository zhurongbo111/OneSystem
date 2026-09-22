using App.Api.Authorization;
using App.Api.Http;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Settlements;
using App.Core.Features.Settlements.CreateSettlement;
using App.Core.Features.Settlements.ExportSettlements;
using App.Core.Features.Settlements.GetReconciliation;
using App.Core.Features.Settlements.GetSettlementById;
using App.Core.Features.Settlements.GetSettlements;
using App.Core.Features.Settlements.GetUnsettledOrders;
using App.Core.Features.Settlements.VoidSettlement;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 收付款控制器（/api/settlements）：列表分页 / 详情 / 新增（核销即生效）/ 作废（回退已结算金额）/
/// 可核销单据候选，以及往来对账（/api/reconciliation）。统一 ApiResponse 包装。
/// </summary>
[Authorize]
[ApiController]
[Route("api/settlements")]
public sealed class SettlementsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化收付款控制器
    /// </summary>
    public SettlementsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询收付款单（含作废单据，作废行前端置灰）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<SettlementListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.SettlementsView)]
    public async Task<ApiResponse<PagedResult<SettlementListItemDto>>> GetPaged([FromQuery] GetSettlementsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 导出收付款单 Excel（当前筛选全量，单据 + 核销明细两个工作表；成功返回文件流）
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致；Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("export")]
    [RequirePermission(Permissions.SettlementsExport)]
    public async Task<IActionResult> Export([FromQuery] ExportSettlementsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 新增收付款单（核销即生效：累加各被核销单据已结算金额）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SettlementDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.SettlementsCreate)]
    public async Task<ApiResponse<SettlementDetailDto>> Create([FromBody] CreateSettlementRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询可核销单据候选（按往来单位 + 方向返回未结单据；固定段路由，注册在 {id} 之前）
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<SettlementCandidateDto>>))]
    [HttpGet("unsettled-orders")]
    [RequirePermission(Permissions.SettlementsView)]
    public async Task<ApiResponse<PagedResult<SettlementCandidateDto>>> GetUnsettledOrders([FromQuery] GetUnsettledOrdersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询收付款单详情（含核销明细，快照字段原样返回）
    /// </summary>
    /// <param name="id">收付款单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SettlementDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.SettlementsView)]
    public async Task<ApiResponse<SettlementDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetSettlementByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 作废收付款单（逐行回退被核销单据已结算金额；仅改状态不删数据）
    /// </summary>
    /// <param name="id">收付款单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SettlementDetailDto>))]
    [HttpPut("{id:guid}/void")]
    [RequirePermission(Permissions.SettlementsVoid)]
    public async Task<ApiResponse<SettlementDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidSettlementRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 往来对账台账（按往来单位聚合应收 / 应付余额与未结单据数）
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<ReconciliationListItemDto>>))]
    [HttpGet("/api/reconciliation")]
    [RequirePermission(Permissions.ReconciliationView)]
    public async Task<ApiResponse<PagedResult<ReconciliationListItemDto>>> GetReconciliation([FromQuery] GetReconciliationRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
