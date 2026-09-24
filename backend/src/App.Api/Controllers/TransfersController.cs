using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Transfers;
using App.Core.Features.Transfers.CreateTransfer;
using App.Core.Features.Transfers.GetTransferById;
using App.Core.Features.Transfers.GetTransfers;
using App.Core.Features.Transfers.VoidTransfer;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 调拨单控制器（/api/transfers）：列表分页 / 详情 / 新增（保存即生效，双仓同事务）/ 作废双向回冲。
/// 调拨不落业务金额、不写凭证、不涉结算（specs/039-erp-transfer/design.md §0）。
/// 统一 ApiResponse 包装；鉴权同既有单据域（全局 [Authorize]，权限由 028-erp-rbac 接入）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/transfers")]
public sealed class TransfersController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化调拨单控制器
    /// </summary>
    public TransfersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询调拨单（含作废单据，作废行前端置灰）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<TransferListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.TransfersView)]
    public async Task<ApiResponse<PagedResult<TransferListItemDto>>> GetPaged([FromQuery] GetTransfersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询调拨单详情（含明细行，快照字段原样返回）
    /// </summary>
    /// <param name="id">调拨单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<TransferDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.TransfersView)]
    public async Task<ApiResponse<TransferDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetTransferByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增调拨单（一步式：保存即生效，转出仓 −、转入仓 + 同一事务）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<TransferDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.TransfersCreate)]
    public async Task<ApiResponse<TransferDetailDto>> Create([FromBody] CreateTransferRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 作废调拨单（双向回冲库存；仅改状态不删数据）
    /// </summary>
    /// <param name="id">调拨单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<TransferDetailDto>))]
    [HttpPut("{id:guid}/void")]
    [RequirePermission(Permissions.TransfersVoid)]
    public async Task<ApiResponse<TransferDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidTransferRequest { Id = id }, cancellationToken));
}
