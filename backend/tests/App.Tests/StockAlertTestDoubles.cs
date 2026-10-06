using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 站内信仓储假实现（041）：内存台账 + 本人过滤 + 已读标记，行为与 EF 实现同口径。
/// </summary>
internal sealed class FakeNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = [];

    /// <summary>当前内存中的全部消息（断言写入条数用）</summary>
    public IReadOnlyList<Notification> All => _notifications;

    /// <summary>批量写入</summary>
    public Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default)
    {
        _notifications.AddRange(notifications);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedAsync(
        Guid userId,
        NotificationType? type,
        bool? isRead,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _notifications.Where(n => n.UserId == userId);

        if (type is not null)
        {
            query = query.Where(n => n.Type == type.Value);
        }

        if (isRead is not null)
        {
            query = isRead.Value ? query.Where(n => n.ReadAt is not null) : query.Where(n => n.ReadAt is null);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(n => n.Title.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(((IReadOnlyList<Notification>)items, ordered.Count));
    }

    /// <inheritdoc />
    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_notifications.Count(n => n.UserId == userId && n.ReadAt is null));

    /// <inheritdoc />
    public Task<bool> MarkReadAsync(Guid id, Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == id && n.UserId == userId);
        if (notification is null)
        {
            return Task.FromResult(false);
        }

        notification.ReadAt ??= readAt;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        var unread = _notifications.Where(n => n.UserId == userId && n.ReadAt is null).ToList();
        foreach (var notification in unread)
        {
            notification.ReadAt = readAt;
        }

        return Task.FromResult(unread.Count);
    }
}

/// <summary>
/// 告警去重台账假实现（041）：内存唯一键集合。
/// </summary>
internal sealed class FakeAlertRecordRepository : IAlertRecordRepository
{
    private readonly HashSet<(AlertType, string, DateOnly)> _keys = [];

    /// <summary>已写入台账条数</summary>
    public int Count => _keys.Count;

    /// <summary>预置一条台账（模拟历史告警）</summary>
    public void Seed(AlertType alertType, string resourceKey, DateOnly alertDate)
        => _keys.Add((alertType, resourceKey, alertDate));

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        AlertType alertType, string resourceKey, DateOnly alertDate, CancellationToken cancellationToken = default)
        => Task.FromResult(_keys.Contains((alertType, resourceKey, alertDate)));

    /// <inheritdoc />
    public Task AddRangeAsync(IReadOnlyList<AlertRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            _keys.Add((record.AlertType, record.ResourceKey, record.AlertDate));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 库存预警只读查询假实现（041）：直接返回预置信号，并记录最近一次的 maxCount。
/// 判定口径由 <see cref="StockAlertQueryRepositoryTests"/> 用真实仓储 + InMemory 覆盖。
/// </summary>
internal sealed class FakeStockAlertQueryRepository : IStockAlertQueryRepository
{
    /// <summary>预置低库存信号</summary>
    public List<StockAlertSignal> LowStock { get; } = [];

    /// <summary>预置近效期信号</summary>
    public List<StockAlertSignal> Expiring { get; } = [];

    /// <summary>预置已过期信号</summary>
    public List<StockAlertSignal> Expired { get; } = [];

    /// <summary>最近一次低库存查询的上限入参</summary>
    public int LastLowStockMaxCount { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<StockAlertSignal>> GetLowStockSignalsAsync(
        int maxCount, CancellationToken cancellationToken = default)
    {
        LastLowStockMaxCount = maxCount;
        return Task.FromResult<IReadOnlyList<StockAlertSignal>>(LowStock.Take(maxCount).ToList());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StockAlertSignal>> GetExpiringBatchSignalsAsync(
        DateOnly today, int nearDays, int maxCount, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StockAlertSignal>>(Expiring.Take(maxCount).ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<StockAlertSignal>> GetExpiredBatchSignalsAsync(
        DateOnly today, int maxCount, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StockAlertSignal>>(Expired.Take(maxCount).ToList());
}

/// <summary>
/// 接收人反查假实现（041）：返回固定用户列表，并记录请求的权限点。
/// </summary>
internal sealed class FakePermissionedUserQuery : IPermissionedUserQuery
{
    /// <summary>预置接收人</summary>
    public List<Guid> UserIds { get; } = [];

    /// <summary>最近一次查询的权限点 key</summary>
    public string? LastPermissionKey { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> GetEnabledUserIdsByPermissionAsync(
        string permissionKey, CancellationToken cancellationToken = default)
    {
        LastPermissionKey = permissionKey;
        return Task.FromResult<IReadOnlyList<Guid>>(UserIds);
    }
}

/// <summary>
/// 站内信写入通道假实现（041）：记录写入的消息（扫描器断言用）。
/// </summary>
internal sealed class FakeNotificationWriter : INotificationWriter
{
    /// <summary>写入的消息（按写入顺序）</summary>
    public List<Notification> Written { get; } = [];

    /// <inheritdoc />
    public Task WriteAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default)
    {
        Written.AddRange(notifications);
        return Task.CompletedTask;
    }
}
