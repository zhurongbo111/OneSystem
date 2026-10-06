using App.Core.Abstractions;

namespace App.Core.Features.Notifications.MarkAllNotificationsRead;

/// <summary>
/// 全部标记已读请求（无请求参数；只影响本人消息）
/// </summary>
public sealed class MarkAllNotificationsReadRequest : IRequest<MarkAllNotificationsReadResponse>
{
}
