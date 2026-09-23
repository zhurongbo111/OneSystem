using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 报价单明细表映射配置（表名 QuotationItems，外键不级联删除，见 specs/037-erp-quotation design.md §2.2）
/// </summary>
internal sealed class QuotationItemConfiguration : IEntityTypeConfiguration<QuotationItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuotationItem> builder)
    {
        builder.ToTable("QuotationItems");
        builder.HasKey(i => i.Id);

        // 明细快照列长度与商品档案同源（ProductFieldConstraints，禁止硬编码）
        builder.Property(i => i.ProductName)
            .HasMaxLength(ProductFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(i => i.Unit)
            .HasMaxLength(ProductFieldConstraints.UnitMaxLength)
            .IsRequired()
            .HasColumnType("varchar(10)");
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPrice).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.Subtotal).IsRequired().HasColumnType("numeric(18,2)");

        builder.HasIndex(i => i.QuotationId);

        // 主表外键：不级联删除（报价单只作废不删除）
        builder.HasOne<Quotation>()
            .WithMany()
            .HasForeignKey(i => i.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
