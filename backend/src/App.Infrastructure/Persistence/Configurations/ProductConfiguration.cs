using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 商品表映射配置（表名 Products，字段长度 / 必填 / 唯一索引 / 外键不级联）
/// </summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        // 长度取自 ProductFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(p => p.Code)
            .HasMaxLength(ProductFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(32)");
        builder.Property(p => p.Name)
            .HasMaxLength(ProductFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(p => p.Unit)
            .HasMaxLength(ProductFieldConstraints.UnitMaxLength)
            .IsRequired()
            .HasColumnType("varchar(10)");
        builder.Property(p => p.PurchasePrice)
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(p => p.SalePrice)
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(p => p.SafetyStock).IsRequired();
        builder.Property(p => p.Status).HasConversion<short>().IsRequired();
        builder.Property(p => p.Remark)
            .HasMaxLength(ProductFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // 编码唯一（大小写敏感；忽略大小写的判定由应用层负责，见 design.md 2.2）
        builder.HasIndex(p => p.Code).IsUnique();

        // 外键不级联删除：商品只停用不删除，分类删除由应用层先校验引用
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
