using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 角色表映射配置（表名 Roles，字段长度 / 必填 / 名称唯一索引）。
/// 名称唯一索引与 exists 判定一致：均按 Trim + 大小写不敏感，重复的机会窗口由 Handler 的 40173 兜底。
/// </summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(RoleFieldConstraints.NameMaxLength).IsRequired();
        builder.Property(r => r.Remark).HasMaxLength(RoleFieldConstraints.RemarkMaxLength);
        builder.Property(r => r.IsBuiltin).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasIndex(r => r.Name).IsUnique();
    }
}
