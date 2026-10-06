using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Responses;

namespace App.Core.Features.Notifications.GetNotifications;

/// <summary>
/// 站内信分页查询用例：只看当前登录用户的消息（不接受 userId 入参，避免越权读他人消息），
/// 支持类型 / 已读状态 / 标题关键词筛选，按生成时间倒序。
/// </summary>
public sealed class GetNotificationsRequestHandler : IRequestHandler<GetNotificationsRequest, PagedResult<NotificationListItemDto>>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化站内信分页查询用例处理器
    /// </summary>
    public GetNotificationsRequestHandler(INotificationRepository notificationRepository, ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理站内信分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<NotificationListItemDto>> HandleAsync(
        GetNotificationsRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "用户信息缺失，请重新登录");

        var type = request.Type is null ? (NotificationType?)null : (NotificationType)request.Type.Value;

        var (items, total) = await _notificationRepository.GetPagedAsync(
            userId,
            type,
            request.IsRead,
            request.Keyword,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<NotificationListItemDto>
        {
            Items = items.Select(NotificationsDtoMapper.ToListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
