using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.GeneralLedger;
using App.Core.Features.Vouchers.CreateVoucher;
using App.Core.Features.Vouchers.GetVoucherById;
using App.Core.Features.Vouchers.GetVouchers;
using App.Core.Features.Vouchers.VoidVoucher;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 记账凭证接口（需登录 + `vouchers.*` 权限点）：分页查询 / 手工录入 / 详情 / 作废
/// </summary>
[Authorize]
[ApiController]
[Route("api/vouchers")]
public class VouchersController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化记账凭证控制器
    /// </summary>
    public VouchersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 凭证分页查询（期间 / 来源类型 / 关键词筛选，含作废凭证）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<VoucherListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.VouchersView)]
    public async Task<ApiResponse<PagedResult<VoucherListItemDto>>> GetVouchers(
        [FromQuery] GetVouchersRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 录入手工凭证（借贷必须平衡；分录科目须为末级且启用；记账日期所在期间须未结账）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<VoucherDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.VouchersCreate)]
    public async Task<ApiResponse<VoucherDetailDto>> CreateVoucher(
        [FromBody] CreateVoucherRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询凭证详情（含分录）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<VoucherDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.VouchersView)]
    public async Task<ApiResponse<VoucherDetailDto>> GetVoucherById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetVoucherByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 作废凭证（已作废幂等拒绝；归属期间已结账禁止作废）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<VoucherDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/void")]
    [RequirePermission(Permissions.VouchersVoid)]
    public async Task<ApiResponse<VoucherDetailDto>> VoidVoucher(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidVoucherRequest { Id = id }, cancellationToken));
}
