using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Notifications.MarkAllNotificationsRead;

/// <summary>
/// 全部标记已读用例：把当前用户全部未读消息置为已读，返回本次标记条数（0 条不算失败）。
/// </summary>
public sealed class MarkAllNotificationsReadRequestHandler
    : IRequestHandler<MarkAllNotificationsReadRequest, MarkAllNotificationsReadResponse>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化全部标记已读用例处理器
    /// </summary>
    public MarkAllNotificationsReadRequestHandler(INotificationRepository notificationRepository, ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理全部标记已读请求
    /// </summary>
    /// <param name="request">空请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<MarkAllNotificationsReadResponse> HandleAsync(
        MarkAllNotificationsReadRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "用户信息缺失，请重新登录");

        var affectedCount = await _notificationRepository.MarkAllReadAsync(userId, DateTimeOffset.UtcNow, cancellationToken);

        return new MarkAllNotificationsReadResponse { AffectedCount = affectedCount };
    }
}
