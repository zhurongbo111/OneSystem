using App.Core.Entities;
using App.Core.Features.Notifications.GetNotifications;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 站内信字段约束一致性测试（041 §2.3 / §6）：
/// ① 列长与 <see cref="NotificationFieldConstraints"/> 常量同源；
/// ② 查询关键词上限 = 实际匹配列（Title）列长，边界值通过 / 越界拒绝；
/// ③ 类型取值只接受已定义枚举值；
/// ④ 去重台账唯一索引 <c>(AlertType, ResourceKey, AlertDate)</c> 存在。
/// </summary>
public class NotificationFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    [Fact]
    public void 标题与内容列长_应等于常量()
    {
        using var dbContext = TestSupport.CreateDbContext();
        Assert.Equal(NotificationFieldConstraints.TitleMaxLength, GetMaxLength<Notification>(dbContext, nameof(Notification.Title)));
        Assert.Equal(NotificationFieldConstraints.ContentMaxLength, GetMaxLength<Notification>(dbContext, nameof(Notification.Content)));
    }

    [Fact]
    public void 资源键与跳转列长_应等于常量()
    {
        using var dbContext = TestSupport.CreateDbContext();
        Assert.Equal(NotificationFieldConstraints.ResourceKeyMaxLength, GetMaxLength<Notification>(dbContext, nameof(Notification.ResourceKey)));
        Assert.Equal(NotificationFieldConstraints.LinkRouteNameMaxLength, GetMaxLength<Notification>(dbContext, nameof(Notification.LinkRouteName)));
        Assert.Equal(NotificationFieldConstraints.LinkQueryMaxLength, GetMaxLength<Notification>(dbContext, nameof(Notification.LinkQuery)));
        Assert.Equal(NotificationFieldConstraints.ResourceKeyMaxLength, GetMaxLength<AlertRecord>(dbContext, nameof(AlertRecord.ResourceKey)));
    }

    [Fact]
    public void 查询关键词长度边界_应与标题列长一致()
    {
        var validator = new GetNotificationsRequestValidator();
        var ok = new string('a', NotificationFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', NotificationFieldConstraints.KeywordMaxLength + 1);

        Assert.True(validator.Validate(new GetNotificationsRequest { Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetNotificationsRequest { Keyword = tooLong }).IsValid);
        Assert.Equal(NotificationFieldConstraints.TitleMaxLength, NotificationFieldConstraints.KeywordMaxLength);
    }

    [Fact]
    public void 分页与类型取值边界_应通过_越界应拒绝()
    {
        var validator = new GetNotificationsRequestValidator();

        Assert.True(validator.Validate(new GetNotificationsRequest { Page = 1, PageSize = 100 }).IsValid);
        Assert.False(validator.Validate(new GetNotificationsRequest { Page = 0 }).IsValid);
        Assert.False(validator.Validate(new GetNotificationsRequest { PageSize = 101 }).IsValid);

        Assert.True(validator.Validate(new GetNotificationsRequest { Type = (int)NotificationType.LowStock }).IsValid);
        // 042 续行：3 = 待审批、4 = 审批结果今为合法取值（原「逾期应收」预留值调整为 5）
        Assert.True(validator.Validate(new GetNotificationsRequest { Type = (int)NotificationType.ApprovalPending }).IsValid);
        Assert.True(validator.Validate(new GetNotificationsRequest { Type = (int)NotificationType.ApprovalDecided }).IsValid);
        Assert.True(validator.Validate(new GetNotificationsRequest { Type = 5 }).IsValid);
        Assert.False(validator.Validate(new GetNotificationsRequest { Type = 6 }).IsValid);
        Assert.False(validator.Validate(new GetNotificationsRequest { Type = 99 }).IsValid);
    }

    [Fact]
    public void 告警去重台账_应存在三列唯一索引()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(AlertRecord))!;

        var uniqueIndex = entityType.GetIndexes()
            .SingleOrDefault(index => index.IsUnique
                && index.Properties.Select(property => property.Name).SequenceEqual(
                    new[] { nameof(AlertRecord.AlertType), nameof(AlertRecord.ResourceKey), nameof(AlertRecord.AlertDate) }));

        Assert.NotNull(uniqueIndex);
    }
}
