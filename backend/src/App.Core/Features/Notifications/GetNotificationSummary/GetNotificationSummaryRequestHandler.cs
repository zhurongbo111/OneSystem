using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Notifications.GetNotificationSummary;

/// <summary>
/// 顶栏未读汇总用例：返回本人未读数与最近 <c>NotificationFieldConstraints.RecentCount</c> 条消息
/// （顶栏铃铛下拉用；前端在路由切换时拉一次，不做轮询）。
/// </summary>
public sealed class GetNotificationSummaryRequestHandler : IRequestHandler<GetNotificationSummaryRequest, NotificationSummaryDto>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化顶栏未读汇总用例处理器
    /// </summary>
    public GetNotificationSummaryRequestHandler(INotificationRepository notificationRepository, ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理顶栏未读汇总请求
    /// </summary>
    /// <param name="request">空请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<NotificationSummaryDto> HandleAsync(
        GetNotificationSummaryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "用户信息缺失，请重新登录");

        var unreadCount = await _notificationRepository.CountUnreadAsync(userId, cancellationToken);

        var (recent, _) = await _notificationRepository.GetPagedAsync(
            userId,
            null,
            null,
            null,
            page: 1,
            pageSize: NotificationFieldConstraints.RecentCount,
            cancellationToken);

        return new NotificationSummaryDto
        {
            UnreadCount = unreadCount,
            Recent = recent.Select(NotificationsDtoMapper.ToListItemDto).ToList(),
        };
    }
}
