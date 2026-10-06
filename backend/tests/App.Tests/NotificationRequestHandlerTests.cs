using App.Core.Abstractions;
using App.Core.Alerts;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Notifications.GetNotifications;
using App.Core.Features.Notifications.GetNotificationSummary;
using App.Core.Features.Notifications.MarkAllNotificationsRead;
using App.Core.Features.Notifications.MarkNotificationRead;
using App.Core.Features.Notifications.ScanStockAlerts;

using Microsoft.Extensions.Logging.Abstractions;

namespace App.Tests;

/// <summary>
/// 站内信用例单测（041 §6）：只看本人 / 筛选分页 / 未读汇总 / 已读与非本人 40400 /
/// 全部已读条数 / 手动扫描统计 / 未登录 40100。
/// </summary>
public class NotificationRequestHandlerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static Notification NewNotification(
        Guid userId,
        NotificationType type = NotificationType.LowStock,
        string title = "库存不足提醒",
        DateTimeOffset? createdAt = null,
        DateTimeOffset? readAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Content = "内容",
            LinkRouteName = "inventory",
            LinkQuery = "{}",
            ResourceKey = "product:1:warehouse:1",
            ReadAt = readAt,
            CreatedAt = createdAt ?? Noon,
        };

    /// <summary>未登录桩（Id 非法 → UserId() 解析失败）</summary>
    private sealed class AnonymousCurrentUser : ICurrentUser
    {
        public string Id => string.Empty;

        public string Username => string.Empty;

        public string DisplayName => string.Empty;
    }

    [Fact]
    public async Task 查询站内信_只看本人且支持筛选与分页()
    {
        var repository = new FakeNotificationRepository();
        var currentUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        await repository.AddRangeAsync(
        [
            NewNotification(currentUser, NotificationType.LowStock, "库存不足提醒", Noon.AddMinutes(1)),
            NewNotification(currentUser, NotificationType.ExpiredBatch, "批次已过期提醒", Noon.AddMinutes(2)),
            NewNotification(currentUser, NotificationType.LowStock, "库存不足提醒-已读", Noon.AddMinutes(3), Noon.AddMinutes(4)),
            NewNotification(otherUser, NotificationType.LowStock, "他人消息", Noon.AddMinutes(5)),
        ]);

        var handler = new GetNotificationsRequestHandler(repository, new StubCurrentUser(currentUser));

        // 只看本人（他人消息存在但不计入）
        var all = await handler.HandleAsync(new GetNotificationsRequest());
        Assert.Equal(3, all.Total);
        // 按生成时间倒序
        Assert.Equal("库存不足提醒-已读", all.Items[0].Title);
        Assert.True(all.Items[0].IsRead);

        var unread = await handler.HandleAsync(new GetNotificationsRequest { IsRead = false });
        Assert.Equal(2, unread.Total);

        var byType = await handler.HandleAsync(new GetNotificationsRequest { Type = (int)NotificationType.LowStock });
        Assert.Equal(2, byType.Total);

        var byKeyword = await handler.HandleAsync(new GetNotificationsRequest { Keyword = "过期" });
        Assert.Equal(1, byKeyword.Total);

        var paged = await handler.HandleAsync(new GetNotificationsRequest { Page = 2, PageSize = 2 });
        Assert.Equal(3, paged.Total);
        Assert.Single(paged.Items);
    }

    [Fact]
    public async Task 查询站内信_未登录_应报未授权()
    {
        var handler = new GetNotificationsRequestHandler(new FakeNotificationRepository(), new AnonymousCurrentUser());

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetNotificationsRequest()));

        Assert.Equal(ErrorCode.Unauthorized, exception.Code);
    }

    [Fact]
    public async Task 未读汇总_应返回未读数与最近五条()
    {
        var repository = new FakeNotificationRepository();
        var currentUser = Guid.NewGuid();
        var notifications = Enumerable.Range(1, 7)
            .Select(index => NewNotification(
                currentUser,
                NotificationType.LowStock,
                $"提醒 {index}",
                Noon.AddMinutes(index),
                index <= 2 ? Noon.AddMinutes(10) : null))
            .ToList();
        await repository.AddRangeAsync(notifications);

        var handler = new GetNotificationSummaryRequestHandler(repository, new StubCurrentUser(currentUser));

        var summary = await handler.HandleAsync(new GetNotificationSummaryRequest());

        Assert.Equal(5, summary.UnreadCount);
        Assert.Equal(NotificationFieldConstraints.RecentCount, summary.Recent.Count);
        Assert.Equal("提醒 7", summary.Recent[0].Title);
    }

    [Fact]
    public async Task 标记已读_本人消息应成功且记录已读时间()
    {
        var repository = new FakeNotificationRepository();
        var currentUser = Guid.NewGuid();
        var notification = NewNotification(currentUser);
        await repository.AddRangeAsync([notification]);

        var handler = new MarkNotificationReadRequestHandler(repository, new StubCurrentUser(currentUser));

        var result = await handler.HandleAsync(new MarkNotificationReadRequest { Id = notification.Id });

        Assert.Null(result);
        Assert.NotNull(notification.ReadAt);
        Assert.Equal(0, await repository.CountUnreadAsync(currentUser));
    }

    [Fact]
    public async Task 标记已读_非本人消息应报资源不存在()
    {
        var repository = new FakeNotificationRepository();
        var notification = NewNotification(Guid.NewGuid());
        await repository.AddRangeAsync([notification]);

        var handler = new MarkNotificationReadRequestHandler(repository, new StubCurrentUser(Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new MarkNotificationReadRequest { Id = notification.Id }));

        Assert.Equal(ErrorCode.NotFound, exception.Code);
    }

    [Fact]
    public async Task 全部已读_应返回标记条数且只影响本人()
    {
        var repository = new FakeNotificationRepository();
        var currentUser = Guid.NewGuid();
        await repository.AddRangeAsync(
        [
            NewNotification(currentUser),
            NewNotification(currentUser, NotificationType.ExpiredBatch),
            NewNotification(Guid.NewGuid()),
        ]);

        var handler = new MarkAllNotificationsReadRequestHandler(repository, new StubCurrentUser(currentUser));

        var result = await handler.HandleAsync(new MarkAllNotificationsReadRequest());

        Assert.Equal(2, result.AffectedCount);
        Assert.Equal(0, await repository.CountUnreadAsync(currentUser));
    }

    [Fact]
    public async Task 手动扫描_应调用扫描器并返回统计()
    {
        var queryRepository = new FakeStockAlertQueryRepository();
        queryRepository.LowStock.Add(new StockAlertSignal
        {
            ProductId = Guid.NewGuid(),
            ProductCode = "SKU-001",
            ProductName = "商品一",
            WarehouseId = Guid.NewGuid(),
            WarehouseName = "上海仓",
            Quantity = 1,
            SafetyStock = 10,
        });
        var permissionedUserQuery = new FakePermissionedUserQuery();
        permissionedUserQuery.UserIds.Add(Guid.NewGuid());
        var scanner = new StockAlertScanner(
            queryRepository,
            new FakeAlertRecordRepository(),
            permissionedUserQuery,
            new FakeNotificationWriter(),
            NullLogger<StockAlertScanner>.Instance);

        var handler = new ScanStockAlertsRequestHandler(scanner);

        var result = await handler.HandleAsync(new ScanStockAlertsRequest());

        Assert.Equal(1, result.SignalCount);
        Assert.Equal(1, result.MessageCount);
        Assert.Equal(1, result.RecipientCount);
    }
}
