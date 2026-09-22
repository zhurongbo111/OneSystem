using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 角色权限关联表映射配置（表名 RolePermissions，复合主键 角色 id + 权限点 key）。
/// 集合型从属表：角色删除时级联清理，更新策略为「先删后插」全量替换。
/// </summary>
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionKey });

        builder.Property(rp => rp.PermissionKey).HasMaxLength(RoleFieldConstraints.PermissionKeyMaxLength).IsRequired();

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
