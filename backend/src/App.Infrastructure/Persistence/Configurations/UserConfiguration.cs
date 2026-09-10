using App.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 用户表映射配置（表名 Users，字段长度 / 必填 / 唯一索引）
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        // 长度取自 UserFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(u => u.Username).HasMaxLength(UserFieldConstraints.UsernameMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnType("text").IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(UserFieldConstraints.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(UserFieldConstraints.EmailMaxLength);
        builder.Property(u => u.Phone).HasMaxLength(UserFieldConstraints.PhoneMaxLength);
        builder.Property(u => u.Status).HasConversion<short>().IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();

        // 登录名唯一（大小写敏感，忽略大小写的判定由应用层负责，见 design.md 2.2）
        builder.HasIndex(u => u.Username).IsUnique();

        // 邮箱 / 手机号可空：仅对非空值建唯一索引（部分索引）
        builder.HasIndex(u => u.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        builder.HasIndex(u => u.Phone).IsUnique().HasFilter("\"Phone\" IS NOT NULL");
    }
}
