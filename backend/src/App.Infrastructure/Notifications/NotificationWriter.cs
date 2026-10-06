using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Infrastructure.Notifications;

/// <summary>
/// 站内信写入通道实现（041-erp-stock-alert §1）：包一层 <see cref="INotificationRepository.AddRangeAsync"/>，
/// 让扫描器与后续通知能力（如 `042` 的待审批提醒）只依赖「写通道」而不依赖具体仓储。
/// </summary>
public sealed class NotificationWriter : INotificationWriter
{
    private readonly INotificationRepository _notificationRepository;

    /// <summary>
    /// 初始化站内信写入通道
    /// </summary>
    public NotificationWriter(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    /// <inheritdoc />
    public Task WriteAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default)
        => notifications.Count == 0
            ? Task.CompletedTask
            : _notificationRepository.AddRangeAsync(notifications, cancellationToken);
}
