using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 审批规则表映射配置（表名 ApprovalRules，specs/042-erp-approval/design.md §2.1）：
/// 按单据类型一条规则（唯一索引），阈值取 numeric(18,2)，默认不启用（迁移不预置数据）。
/// </summary>
internal sealed class ApprovalRuleConfiguration : IEntityTypeConfiguration<ApprovalRule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalRule> builder)
    {
        builder.ToTable("ApprovalRules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.OrderType).HasConversion<short>().IsRequired();
        builder.Property(r => r.ThresholdAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(r => r.Enabled).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        // 单据类型唯一：一类单据一条规则
        builder.HasIndex(r => r.OrderType).IsUnique();
    }
}
