using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 会计科目表映射配置（表名 Accounts，编码唯一索引 / 上级自引用外键不级联 / 同级排序默认 0）
/// （specs/031-erp-finance-master/design.md §2.1）。
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);

        // 长度取自 AccountFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(a => a.Code)
            .HasMaxLength(AccountFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(a => a.Name)
            .HasMaxLength(AccountFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(a => a.Category).HasConversion<short>().IsRequired();
        builder.Property(a => a.Direction).HasConversion<short>().IsRequired();
        builder.Property(a => a.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(a => a.IsPreset).IsRequired().HasDefaultValue(false);
        // 不设数据库默认值：状态由实体属性默认值（Enabled）决定，避免 'Disabled'(0) 命中 CLR 默认值被库默认覆盖
        builder.Property(a => a.Status).HasConversion<short>().IsRequired();
        builder.Property(a => a.Remark)
            .HasMaxLength(AccountFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        // 编码唯一（大小写敏感；忽略大小写的判定由应用层负责，同 specs/030-erp-org-employee/design.md §0.3）
        builder.HasIndex(a => a.Code).IsUnique();

        // 上级索引（建树 / 上溯 / 子科目判定）
        builder.HasIndex(a => a.ParentId);

        // 自引用外键不级联删除：删除保护（有子科目禁止删除）由应用层先校验
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(a => a.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}