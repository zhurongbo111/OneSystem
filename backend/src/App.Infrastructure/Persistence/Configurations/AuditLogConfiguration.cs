using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 操作审计日志表映射配置（表名 AuditLogs；纯追加，无更新 / 删除）。
/// 外键一律不建：操作人可被停用、业务对象跨多表，日志必须自包含且永不被级联影响。
/// </summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserId);
        builder.Property(l => l.Username).HasMaxLength(AuditLogFieldConstraints.UsernameMaxLength);
        builder.Property(l => l.DisplayName).HasMaxLength(AuditLogFieldConstraints.DisplayNameMaxLength);
        builder.Property(l => l.Resource).HasColumnType("smallint").IsRequired();
        builder.Property(l => l.Action).HasColumnType("smallint").IsRequired();
        builder.Property(l => l.ResourceId);
        builder.Property(l => l.ResourceNo).HasMaxLength(AuditLogFieldConstraints.ResourceNoMaxLength);
        builder.Property(l => l.Summary).HasMaxLength(AuditLogFieldConstraints.SummaryMaxLength).IsRequired();
        builder.Property(l => l.Changes).HasColumnType("jsonb");
        builder.Property(l => l.CreatedAt).IsRequired();

        // 列表默认倒序 / 按对象追溯 / 按人追溯 / 按业务标识查询
        builder.HasIndex(l => l.CreatedAt).IsDescending();
        builder.HasIndex(l => new { l.Resource, l.ResourceId });
        builder.HasIndex(l => l.UserId);
        builder.HasIndex(l => l.ResourceNo);
    }
}
