using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.BankAccounts;
using App.Core.Features.BankAccounts.CreateBankAccount;
using App.Core.Features.BankAccounts.DeleteBankAccount;
using App.Core.Features.BankAccounts.GetBankAccountById;
using App.Core.Features.BankAccounts.GetBankAccountSummary;
using App.Core.Features.BankAccounts.GetBankAccounts;
using App.Core.Features.BankAccounts.UpdateBankAccount;
using App.Core.Features.BankAccounts.UpdateBankAccountStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 资金账户接口（需登录 + `bankAccounts.*` 权限点）：资金账户维护（新增 / 编辑 / 启停 / 删除）与余额总览
/// </summary>
[Authorize]
[ApiController]
[Route("api/bank-accounts")]
public class BankAccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化资金账户控制器
    /// </summary>
    public BankAccountsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询资金账户（支持编码 / 名称关键词与类型 / 状态筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<BankAccountListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.BankAccountsView)]
    public async Task<ApiResponse<PagedResult<BankAccountListItemDto>>> GetBankAccounts(
        [FromQuery] GetBankAccountsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增资金账户（编码全局唯一）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BankAccountDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.BankAccountsCreate)]
    public async Task<ApiResponse<BankAccountDetailDto>> CreateBankAccount(
        [FromBody] CreateBankAccountRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 各资金账户当前余额总览（初始余额 + Σ 收款 − Σ 付款）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<BankAccountBalanceItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("summary")]
    [RequirePermission(Permissions.BankAccountsView)]
    public async Task<ApiResponse<IReadOnlyList<BankAccountBalanceItemDto>>> GetBankAccountSummary(
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetBankAccountSummaryRequest(), cancellationToken));

    /// <summary>
    /// 查询资金账户详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BankAccountDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.BankAccountsView)]
    public async Task<ApiResponse<BankAccountDetailDto>> GetBankAccountById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetBankAccountByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑资金账户（编码 / 名称 / 类型 / 开户行 / 账号 / 初始余额 / 状态 / 备注全量覆盖）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BankAccountDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.BankAccountsUpdate)]
    public async Task<ApiResponse<BankAccountDetailDto>> UpdateBankAccount(
        [FromRoute] Guid id,
        [FromBody] UpdateBankAccountRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateBankAccountRequest
        {
            Id = id,
            Code = request.Code,
            Name = request.Name,
            Type = request.Type,
            BankName = request.BankName,
            AccountNo = request.AccountNo,
            InitialBalance = request.InitialBalance,
            Status = request.Status,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除资金账户（已被收付款单引用时返回 40161）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object?>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.BankAccountsDelete)]
    public async Task<ApiResponse<object?>> DeleteBankAccount(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeleteBankAccountRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 启用 / 停用资金账户（停用账户不可被新收付款单引用）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BankAccountDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.BankAccountsStatus)]
    public async Task<ApiResponse<BankAccountDetailDto>> UpdateBankAccountStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateBankAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBankAccountStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
