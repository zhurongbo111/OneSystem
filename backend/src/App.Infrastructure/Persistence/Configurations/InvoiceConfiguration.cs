using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 发票主表映射配置（表名 Invoices，发票号唯一索引；外键不级联删除，业务对象只作废不删除）。
/// 长度 / 精度取自 InvoiceFieldConstraints 等常量（单一来源，见 design.md §2.4）。
/// </summary>
internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNo)
            .HasMaxLength(InvoiceFieldConstraints.InvoiceNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(i => i.Type).HasConversion<short>().IsRequired();
        builder.Property(i => i.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(i => i.InvoiceDate).IsRequired();
        builder.Property(i => i.AmountExcludingTax).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.TaxRate)
            .HasPrecision(InvoiceFieldConstraints.TaxRatePrecision, InvoiceFieldConstraints.TaxRateDecimalPlaces)
            .IsRequired();
        builder.Property(i => i.TaxAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.TotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.Status).HasConversion<short>().IsRequired();
        builder.Property(i => i.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        // 发票号全局唯一（并发兜底：冲突由仓储包装为 40132，见 design.md §3.1）
        builder.HasIndex(i => i.InvoiceNo).IsUnique();

        // 往来单位外键：不级联删除（往来单位只停用不删除）
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(i => i.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}