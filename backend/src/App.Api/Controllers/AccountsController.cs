using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Accounts;
using App.Core.Features.Accounts.CreateAccount;
using App.Core.Features.Accounts.DeleteAccount;
using App.Core.Features.Accounts.GetAccountById;
using App.Core.Features.Accounts.GetAccounts;
using App.Core.Features.Accounts.UpdateAccount;
using App.Core.Features.Accounts.UpdateAccountStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 会计科目接口（需登录 + `accounts.*` 权限点）：科目树维护（新增 / 编辑 / 启停 / 删除）
/// </summary>
[Authorize]
[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化会计科目控制器
    /// </summary>
    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询会计科目树（科目量级小，一次返回全量树，不在服务端分页）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<AccountTreeNodeDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.AccountsView)]
    public async Task<ApiResponse<IReadOnlyList<AccountTreeNodeDto>>> GetAccounts(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetAccountsRequest(), cancellationToken));

    /// <summary>
    /// 新增会计科目（编码全局唯一；上级可空表示一级科目）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<AccountDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.AccountsCreate)]
    public async Task<ApiResponse<AccountDetailDto>> CreateAccount(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询会计科目详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<AccountDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AccountsView)]
    public async Task<ApiResponse<AccountDetailDto>> GetAccountById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetAccountByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑会计科目（编码 / 名称 / 类别 / 方向 / 上级 / 排序 / 状态 / 备注全量覆盖；
    /// 上级不得为自身或自身下级；是否预置科目不可改）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<AccountDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AccountsUpdate)]
    public async Task<ApiResponse<AccountDetailDto>> UpdateAccount(
        [FromRoute] Guid id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateAccountRequest
        {
            Id = id,
            Code = request.Code,
            Name = request.Name,
            Category = request.Category,
            Direction = request.Direction,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            Status = request.Status,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除会计科目（预置科目、有子科目、已被凭证引用时禁止删除）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object?>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.AccountsDelete)]
    public async Task<ApiResponse<object?>> DeleteAccount(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeleteAccountRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 启用 / 停用会计科目（停用科目不可被新凭证引用）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<AccountDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.AccountsStatus)]
    public async Task<ApiResponse<AccountDetailDto>> UpdateAccountStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAccountStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}