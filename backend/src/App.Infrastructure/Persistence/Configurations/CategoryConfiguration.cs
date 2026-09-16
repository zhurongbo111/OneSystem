using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 商品分类表映射配置（表名 Categories，字段长度 / 必填 / 唯一索引）
/// </summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        // 长度取自 CategoryFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(c => c.Name)
            .HasMaxLength(CategoryFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(c => c.CreatedAt).IsRequired();

        // 名称唯一（大小写敏感；忽略大小写的判定由应用层负责，见 design.md 2.1）
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
