using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 跟进活动表映射配置（表名 Activities，纯追加表：只增不改不删，故无 UpdatedAt / UpdatedBy）。
/// 列长统一取自 <see cref="ActivityFieldConstraints"/>（单一来源，specs/043-erp-crm-presale design.md §2.3 / §2.4）。
/// </summary>
internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.BizType).HasConversion<short>().IsRequired();
        builder.Property(a => a.Type).HasConversion<short>().IsRequired();
        builder.Property(a => a.Content)
            .HasMaxLength(ActivityFieldConstraints.ContentMaxLength)
            .IsRequired()
            .HasColumnType("varchar(200)");
        builder.Property(a => a.ActivityTime).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        // 归属查询（线索 / 商机详情的时间线）
        builder.HasIndex(a => a.BizId);
    }
}
