using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 站内信仓储的 EF Core 实现（PostgreSQL，041-erp-stock-alert）。只做数据访问，不做业务判定；
/// 全部读取方法按 <c>userId</c> 限定本人消息，标记已读同样限定本人（越过本人视为未命中）。
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化站内信仓储
    /// </summary>
    public NotificationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0)
        {
            return;
        }

        _dbContext.Notifications.AddRange(notifications);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedAsync(
        Guid userId,
        NotificationType? type,
        bool? isRead,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(n => n.Type == value);
        }

        if (isRead is not null)
        {
            query = isRead.Value
                ? query.Where(n => n.ReadAt != null)
                : query.Where(n => n.ReadAt == null);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 关键词只匹配标题列（列长 50 与 NotificationFieldConstraints.KeywordMaxLength 一致）
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(n => n.Title.ToLower().Contains(lower));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> MarkReadAsync(
        Guid id, Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (notification is null)
        {
            return false;
        }

        // 已读时间不被后一次点击覆盖（保留首次已读时间）
        if (notification.ReadAt is null)
        {
            notification.ReadAt = readAt;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        var unread = await _dbContext.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return 0;
        }

        foreach (var notification in unread)
        {
            notification.ReadAt = readAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }
}
