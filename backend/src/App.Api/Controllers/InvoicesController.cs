using App.Api.Authorization;
using App.Api.Http;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Invoices;
using App.Core.Features.Invoices.CreateInvoice;
using App.Core.Features.Invoices.ExportInvoices;
using App.Core.Features.Invoices.GetInvoiceById;
using App.Core.Features.Invoices.GetInvoices;
using App.Core.Features.Invoices.GetInvoicableOrders;
using App.Core.Features.Invoices.VoidInvoice;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 发票控制器（/api/invoices）：列表分页 / 导出 / 登记（关联单据 + 金额闸门）/ 详情 / 作废 /
/// 可开票单据候选。统一 ApiResponse 包装（导出接口为文件下载契约例外，见 `AGENTS.md` §4.1）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化发票控制器
    /// </summary>
    public InvoicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询发票（含作废发票，作废行前端置灰）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<InvoiceListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.InvoicesView)]
    public async Task<ApiResponse<PagedResult<InvoiceListItemDto>>> GetPaged([FromQuery] GetInvoicesRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 导出发票 Excel（当前筛选全量，发票 + 关联明细两个工作表；成功返回文件流）
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致；Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("export")]
    [RequirePermission(Permissions.InvoicesExport)]
    public async Task<IActionResult> Export([FromQuery] ExportInvoicesRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 登记发票（税额与价税合计由后端按税率重算；每行开票金额不得超过该单据未开票金额）
    /// </summary>
    /// <param name="request">登记请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<InvoiceDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.InvoicesCreate)]
    public async Task<ApiResponse<InvoiceDetailDto>> Create([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询可开票单据候选（按往来单位 + 发票类型返回未开票单据；固定段路由，注册在 {id} 之前）
    /// </summary>
    /// <param name="request">查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<InvoicableOrderDto>>))]
    [HttpGet("invoicable-orders")]
    [RequirePermission(Permissions.InvoicesView)]
    public async Task<ApiResponse<PagedResult<InvoicableOrderDto>>> GetInvoicableOrders([FromQuery] GetInvoicableOrdersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询发票详情（含关联单据明细，快照字段原样返回）
    /// </summary>
    /// <param name="id">发票 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<InvoiceDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.InvoicesView)]
    public async Task<ApiResponse<InvoiceDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetInvoiceByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 作废发票（仅改状态不删数据；占用金额按聚合自动释放）
    /// </summary>
    /// <param name="id">发票 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<InvoiceDetailDto>))]
    [HttpPut("{id:guid}/void")]
    [RequirePermission(Permissions.InvoicesVoid)]
    public async Task<ApiResponse<InvoiceDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidInvoiceRequest { Id = id }, cancellationToken));
}