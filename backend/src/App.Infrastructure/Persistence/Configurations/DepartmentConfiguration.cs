using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 部门表映射配置（表名 Departments，编码唯一索引 / 上级自引用外键不级联 / 同级排序默认 0）
/// </summary>
internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);

        // 长度取自 DepartmentFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(d => d.Code)
            .HasMaxLength(DepartmentFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(d => d.Name)
            .HasMaxLength(DepartmentFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(d => d.SortOrder).IsRequired().HasDefaultValue(0);
        // 不设数据库默认值：状态由实体属性默认值（Enabled）决定，避免 'Disabled'(0) 命中 CLR 默认值被库默认覆盖
        builder.Property(d => d.Status).HasConversion<short>().IsRequired();
        builder.Property(d => d.Remark)
            .HasMaxLength(DepartmentFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();

        // 编码唯一（大小写敏感；忽略大小写的判定由应用层负责，见 specs/030-erp-org-employee/design.md §0.3）
        builder.HasIndex(d => d.Code).IsUnique();

        // 上级索引（建树 / 上溯 / 子部门判定）
        builder.HasIndex(d => d.ParentId);

        // 自引用外键不级联删除：删除保护（有子部门禁止删除）由应用层先校验
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(d => d.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
