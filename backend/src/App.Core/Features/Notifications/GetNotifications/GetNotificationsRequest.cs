using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Notifications.GetNotifications;

/// <summary>
/// 站内信分页查询请求（类型 / 已读状态 / 标题关键词筛选；只看本人消息，不接受 userId 入参）
/// </summary>
public sealed class GetNotificationsRequest : IRequest<PagedResult<NotificationListItemDto>>
{
    /// <summary>消息类型（0 低库存 / 1 近效期 / 2 过期，可空 = 全部）</summary>
    public int? Type { get; init; }

    /// <summary>已读状态（true 已读 / false 未读，可空 = 全部）</summary>
    public bool? IsRead { get; init; }

    /// <summary>标题关键词（模糊匹配，可空）</summary>
    public string? Keyword { get; init; }

    /// <summary>页码（从 1 起）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（默认 20，上限 100）</summary>
    public int PageSize { get; init; } = 20;
}
