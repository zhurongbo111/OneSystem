using App.Api.Authorization;

using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Notifications;
using App.Core.Features.Notifications.GetNotifications;
using App.Core.Features.Notifications.GetNotificationSummary;
using App.Core.Features.Notifications.MarkAllNotificationsRead;
using App.Core.Features.Notifications.MarkNotificationRead;
using App.Core.Features.Notifications.ScanStockAlerts;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 站内消息接口（需登录，041-erp-stock-alert）：本人消息查询 / 未读汇总 / 标记已读 / 手动触发库存预警扫描。
/// 全部读取接口只看本人消息（id 取自登录态，不接受 userId 入参）；固定段路由置于 <c>{id:guid}</c> 之前。
/// </summary>
[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化站内消息控制器
    /// </summary>
    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询本人站内信（类型 / 已读状态 / 标题关键词筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<NotificationListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.NotificationsView)]
    public async Task<ApiResponse<PagedResult<NotificationListItemDto>>> GetNotifications(
        [FromQuery] GetNotificationsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 未读汇总（未读数 + 最近若干条本人消息，顶栏铃铛用）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<NotificationSummaryDto>))]
    [HttpGet("summary")]
    [RequirePermission(Permissions.NotificationsView)]
    public async Task<ApiResponse<NotificationSummaryDto>> GetNotificationSummary(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetNotificationSummaryRequest(), cancellationToken));

    /// <summary>
    /// 单条标记已读（仅本人消息；非本人 / 不存在返回 40400）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object?>))]
    [HttpPut("{id:guid}/read")]
    [RequirePermission(Permissions.NotificationsView)]
    public async Task<ApiResponse<object?>> MarkNotificationRead([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new MarkNotificationReadRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 全部标记已读（只影响本人未读消息，返回本次标记条数）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<MarkAllNotificationsReadResponse>))]
    [HttpPut("read-all")]
    [RequirePermission(Permissions.NotificationsView)]
    public async Task<ApiResponse<MarkAllNotificationsReadResponse>> MarkAllNotificationsRead(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new MarkAllNotificationsReadRequest(), cancellationToken));

    /// <summary>
    /// 手动触发库存预警扫描（与定时宿主共用同一实现；无业务失败分支，返回扫描统计）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<StockAlertScanResultDto>))]
    [HttpPost("scan")]
    [RequirePermission(Permissions.NotificationsScan)]
    public async Task<ApiResponse<StockAlertScanResultDto>> ScanStockAlerts(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new ScanStockAlertsRequest(), cancellationToken));
}
