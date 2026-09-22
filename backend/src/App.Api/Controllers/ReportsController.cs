using App.Api.Authorization;
using App.Api.Http;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Reports;
using App.Core.Features.Reports.ExportCostProfit;
using App.Core.Features.Reports.ExportInventoryFlow;
using App.Core.Features.Reports.ExportPurchaseSummary;
using App.Core.Features.Reports.ExportSalesSummary;
using App.Core.Features.Reports.ExportStockBalance;
using App.Core.Features.Reports.GetCostProfitReport;
using App.Core.Features.Reports.GetInventoryFlow;
using App.Core.Features.Reports.GetPurchaseSummary;
using App.Core.Features.Reports.GetSalesSummary;
using App.Core.Features.Reports.GetStockBalance;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 进销存报表接口（需登录）：4 个只读查询，按请求实时聚合，不落物化表、不新增写路径
/// </summary>
[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化报表控制器
    /// </summary>
    /// <param name="mediator">用例中介</param>
    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 进销存报表：期间 +（商品 / 分类 / 只看有变动）筛选，返回期初 / 期间入 / 期间出 / 期末四列与全量合计
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ReportPageDto<InventoryFlowItemDto, InventoryFlowSummaryDto>>))]
    [HttpGet("inventory-flow")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ApiResponse<ReportPageDto<InventoryFlowItemDto, InventoryFlowSummaryDto>>> GetInventoryFlow(
        [FromQuery] GetInventoryFlowRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 库存余额表：启用商品按分类聚合（商品数 / 库存合计 / 零库存数 / 低库存数 / 占比），明细走库存查询页
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ReportPageDto<StockBalanceItemDto, StockBalanceSummaryDto>>))]
    [HttpGet("stock-balance")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ApiResponse<ReportPageDto<StockBalanceItemDto, StockBalanceSummaryDto>>> GetStockBalance(
        [FromQuery] GetStockBalanceRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 采购汇总：期间内未作废入库单与退货单按供应商 / 商品维度聚合，输出净数量与净额
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ReportPageDto<PurchaseSummaryItemDto, PurchaseSummaryTotalDto>>))]
    [HttpGet("purchase-summary")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ApiResponse<ReportPageDto<PurchaseSummaryItemDto, PurchaseSummaryTotalDto>>> GetPurchaseSummary(
        [FromQuery] GetPurchaseSummaryRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 销售汇总：期间内未作废出库单与退货单按客户 / 商品维度聚合，输出净数量与净额
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ReportPageDto<SalesSummaryItemDto, SalesSummaryTotalDto>>))]
    [HttpGet("sales-summary")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ApiResponse<ReportPageDto<SalesSummaryItemDto, SalesSummaryTotalDto>>> GetSalesSummary(
        [FromQuery] GetSalesSummaryRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 成本与毛利报表：期间内销售出库与退货按单据 / 商品 / 往来单位维度聚合收入、成本、毛利、毛利率
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ReportPageDto<CostProfitItemDto, CostProfitTotalDto>>))]
    [HttpGet("cost-profit")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ApiResponse<ReportPageDto<CostProfitItemDto, CostProfitTotalDto>>> GetCostProfitReport(
        [FromQuery] GetCostProfitReportRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 进销存报表导出：期间 +（商品 / 分类 / 只看有变动）筛选，导出列 = 报表列定义，末行为全量口径合计
    /// </summary>
    /// <param name="request">导出请求（Query 绑定，筛选参数与报表查询一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("inventory-flow/export")]
    [RequirePermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportInventoryFlow(
        [FromQuery] ExportInventoryFlowRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 库存余额表导出：按分类聚合列集（含库存金额 / 均价 / 成本异常）产出 xlsx，末行为全量口径合计
    /// </summary>
    /// <param name="request">导出请求（Query 绑定，筛选参数与报表查询一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("stock-balance/export")]
    [RequirePermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportStockBalance(
        [FromQuery] ExportStockBalanceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 采购汇总导出：按供应商 / 商品维度列集产出 xlsx，末行为全量口径合计（含净数量与净额）
    /// </summary>
    /// <param name="request">导出请求（Query 绑定，筛选参数与报表查询一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("purchase-summary/export")]
    [RequirePermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportPurchaseSummary(
        [FromQuery] ExportPurchaseSummaryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 销售汇总导出：按客户 / 商品维度列集产出 xlsx，末行为全量口径合计（含净数量与净额）
    /// </summary>
    /// <param name="request">导出请求（Query 绑定，筛选参数与报表查询一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("sales-summary/export")]
    [RequirePermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportSalesSummary(
        [FromQuery] ExportSalesSummaryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 成本与毛利报表导出：按单据 / 商品 / 往来单位维度列集（含毛利 / 毛利率 / 成本完整性）产出 xlsx，末行为全量口径合计
    /// </summary>
    /// <param name="request">导出请求（Query 绑定，筛选参数与报表查询一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("cost-profit/export")]
    [RequirePermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportCostProfit(
        [FromQuery] ExportCostProfitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }
}
