using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 站内信仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，041-erp-stock-alert）。
/// 只追加与标记已读：无删除 / 编辑；全部读取方法只查本人消息（<c>userId</c> 由 Handler 取当前用户传入）。
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// 批量写入站内信（扫描一次生成的多条消息）
    /// </summary>
    /// <param name="notifications">待写入消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default);

    /// <summary>
    /// 本人消息分页查询：<paramref name="type"/> / <paramref name="isRead"/> 可空精确筛选，
    /// <paramref name="keyword"/> 模糊匹配标题；按 CreatedAt 倒序；<c>AsNoTracking</c>。
    /// </summary>
    /// <param name="userId">接收人用户 id（当前用户）</param>
    /// <param name="type">消息类型，可空</param>
    /// <param name="isRead">是否已读（true 只看已读 / false 只看未读 / 空 = 全部）</param>
    /// <param name="keyword">标题关键词，可空</param>
    /// <param name="page">页码（从 1 起）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedAsync(
        Guid userId,
        NotificationType? type,
        bool? isRead,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 本人未读消息数
    /// </summary>
    /// <param name="userId">接收人用户 id（当前用户）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 单条标记已读（仅本人消息；返回是否命中，未命中 = 不存在或非本人）
    /// </summary>
    /// <param name="id">站内信 id</param>
    /// <param name="userId">接收人用户 id（当前用户）</param>
    /// <param name="readAt">已读时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> MarkReadAsync(
        Guid id, Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 本人全部未读标记已读
    /// </summary>
    /// <param name="userId">接收人用户 id（当前用户）</param>
    /// <param name="readAt">已读时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次标记条数</returns>
    Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default);
}
