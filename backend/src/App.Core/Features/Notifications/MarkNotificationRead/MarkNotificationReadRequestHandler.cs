using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Notifications.MarkNotificationRead;

/// <summary>
/// 单条标记已读用例：按 id + 当前用户定位消息（非本人视为不存在 → <c>40400</c>），
/// 已读时间只写首次点击（重复点击不覆盖已读时间）。
/// </summary>
public sealed class MarkNotificationReadRequestHandler : IRequestHandler<MarkNotificationReadRequest, object?>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化单条标记已读用例处理器
    /// </summary>
    public MarkNotificationReadRequestHandler(INotificationRepository notificationRepository, ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理单条标记已读请求
    /// </summary>
    /// <param name="request">标记已读请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(MarkNotificationReadRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "用户信息缺失，请重新登录");

        var found = await _notificationRepository.MarkReadAsync(
            request.Id, userId, DateTimeOffset.UtcNow, cancellationToken);

        if (!found)
        {
            throw new BusinessException(ErrorCode.NotFound, "站内信不存在");
        }

        return null;
    }
}
