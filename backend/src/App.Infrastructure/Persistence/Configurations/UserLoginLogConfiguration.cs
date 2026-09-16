using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 登录日志表映射配置（表名 UserLoginLogs；只追加，无更新 / 删除）
/// </summary>
internal sealed class UserLoginLogConfiguration : IEntityTypeConfiguration<UserLoginLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserLoginLog> builder)
    {
        builder.ToTable("UserLoginLogs");
        builder.HasKey(l => l.Id);

        // 长度取自 UserFieldConstraints（单一来源），快照列与 Users 同名列表长度一致
        builder.Property(l => l.Username).HasMaxLength(UserFieldConstraints.UsernameMaxLength).IsRequired();
        builder.Property(l => l.DisplayName).HasMaxLength(UserFieldConstraints.DisplayNameMaxLength).IsRequired();
        builder.Property(l => l.LoginAt).IsRequired();
        builder.Property(l => l.IpAddress).HasMaxLength(UserFieldConstraints.IpAddressMaxLength);
        builder.Property(l => l.UserAgent).HasMaxLength(UserFieldConstraints.UserAgentMaxLength);

        // 列表默认按登录时间倒序
        builder.HasIndex(l => l.LoginAt).IsDescending();
        // 外键索引（PostgreSQL 不自动为外键建索引）
        builder.HasIndex(l => l.UserId);

        // 用户不可删除 → 外键禁止级联删除
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
