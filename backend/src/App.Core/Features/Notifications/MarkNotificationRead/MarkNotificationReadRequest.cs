using App.Core.Abstractions;

namespace App.Core.Features.Notifications.MarkNotificationRead;

/// <summary>
/// 单条标记已读请求（id 由路由提供，仅能标记本人消息）
/// </summary>
public sealed class MarkNotificationReadRequest : IRequest<object?>
{
    /// <summary>站内信 ID（路由参数，可缺省以兼容请求体绑定约定）</summary>
    public Guid Id { get; init; }
}
