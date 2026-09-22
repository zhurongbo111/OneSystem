using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.AccountMappings.GetAccountMappings;
using App.Core.Features.AccountMappings.UpdateAccountMappings;
using App.Core.Features.GeneralLedger;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 科目映射接口（需登录 + `vouchers.*` 权限点）：业务事件 → 会计科目的映射维护，
/// 自动凭证按映射取科目（缺失映射拒绝生成）
/// </summary>
[Authorize]
[ApiController]
[Route("api/account-mappings")]
public class AccountMappingsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化科目映射控制器
    /// </summary>
    public AccountMappingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询科目映射（返回全部映射键，未配置的键科目字段为空串）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<AccountMappingDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.VouchersView)]
    public async Task<ApiResponse<IReadOnlyList<AccountMappingDto>>> GetAccountMappings(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetAccountMappingsRequest(), cancellationToken));

    /// <summary>
    /// 维护科目映射（全量覆盖，须提交全部映射键；目标科目须为末级且启用）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<AccountMappingDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut]
    [RequirePermission(Permissions.VouchersUpdateMapping)]
    public async Task<ApiResponse<IReadOnlyList<AccountMappingDto>>> UpdateAccountMappings(
        [FromBody] UpdateAccountMappingsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
