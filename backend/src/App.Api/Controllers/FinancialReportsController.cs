using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.FinancialReports.GetAccountBalance;
using App.Core.Features.FinancialReports.GetBalanceSheet;
using App.Core.Features.FinancialReports.GetIncomeStatement;
using App.Core.Features.GeneralLedger;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 财务报表接口（需登录 + `financialReports.view` 权限点）：
/// 科目余额表 / 资产负债表 / 利润表，按期间实时聚合、不落物化表
/// </summary>
[Authorize]
[ApiController]
[Route("api/reports")]
public class FinancialReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化财务报表控制器
    /// </summary>
    public FinancialReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 科目余额表：按期间输出一级科目的期初 / 本期借贷发生额 / 期末余额
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<AccountBalanceItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("account-balance")]
    [RequirePermission(Permissions.FinancialReportsView)]
    public async Task<ApiResponse<IReadOnlyList<AccountBalanceItemDto>>> GetAccountBalance(
        [FromQuery] GetAccountBalanceRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 资产负债表：资产（含成本类）/ 负债 / 权益三块 + 损益类构成的「本年利润」行
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BalanceSheetDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("balance-sheet")]
    [RequirePermission(Permissions.FinancialReportsView)]
    public async Task<ApiResponse<BalanceSheetDto>> GetBalanceSheet(
        [FromQuery] GetBalanceSheetRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 利润表：损益类科目本期发生额，按收入 / 成本费用两侧列示并给出净利润
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IncomeStatementDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("income-statement")]
    [RequirePermission(Permissions.FinancialReportsView)]
    public async Task<ApiResponse<IncomeStatementDto>> GetIncomeStatement(
        [FromQuery] GetIncomeStatementRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
