using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 告警去重台账表映射配置（表名 AlertRecords，specs/041-erp-stock-alert/design.md §2.2）：
/// 唯一索引 <c>(AlertType, ResourceKey, AlertDate)</c> 为「同日一次」的库层兜底；
/// 台账只增不改、无审计字段。
/// </summary>
internal sealed class AlertRecordConfiguration : IEntityTypeConfiguration<AlertRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AlertRecord> builder)
    {
        builder.ToTable("AlertRecords");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AlertType).HasConversion<short>().IsRequired();
        builder.Property(a => a.ResourceKey)
            .HasMaxLength(NotificationFieldConstraints.ResourceKeyMaxLength)
            .IsRequired()
            .HasColumnType("varchar(100)");
        builder.Property(a => a.AlertDate).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        // 去重兜底：同一信号对象 + 类型同一天只告警一次（并发重复扫描由数据库拒绝）
        builder.HasIndex(a => new { a.AlertType, a.ResourceKey, a.AlertDate }).IsUnique();
    }
}
