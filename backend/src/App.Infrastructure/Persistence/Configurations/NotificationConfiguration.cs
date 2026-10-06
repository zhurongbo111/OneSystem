using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 站内信表映射配置（表名 Notifications，specs/041-erp-stock-alert/design.md §2.1）：
/// 列长取自 <see cref="NotificationFieldConstraints"/>（单一来源）；
/// 只追加与标记已读，无软删除；外键不级联（用户不删除，只停用）。
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.Type).HasConversion<short>().IsRequired();
        builder.Property(n => n.Title)
            .HasMaxLength(NotificationFieldConstraints.TitleMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(n => n.Content)
            .HasMaxLength(NotificationFieldConstraints.ContentMaxLength)
            .IsRequired()
            .HasColumnType("varchar(500)");
        builder.Property(n => n.LinkRouteName)
            .HasMaxLength(NotificationFieldConstraints.LinkRouteNameMaxLength)
            .HasColumnType("varchar(50)");
        builder.Property(n => n.LinkQuery)
            .HasMaxLength(NotificationFieldConstraints.LinkQueryMaxLength)
            .HasColumnType("varchar(500)");
        builder.Property(n => n.ResourceKey)
            .HasMaxLength(NotificationFieldConstraints.ResourceKeyMaxLength)
            .HasColumnType("varchar(100)");
        builder.Property(n => n.ReadAt).IsRequired(false);
        builder.Property(n => n.CreatedAt).IsRequired();

        // 未读查询（本人 + 已读状态）与列表查询（本人 + 时间倒序）
        builder.HasIndex(n => new { n.UserId, n.ReadAt });
        builder.HasIndex(n => new { n.UserId, n.CreatedAt }).IsDescending(false, true);

        // 类型筛选
        builder.HasIndex(n => n.Type);

        // 外键不级联删除：通知属于接收人，用户只停用不删除
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
