using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 报价单表映射配置（表名 Quotations，单号唯一索引；外键不级联删除，报价单只作废不删除）
/// </summary>
internal sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations");
        builder.HasKey(q => q.Id);

        // 长度取自 OrderFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(q => q.QuotationNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(q => q.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(q => q.QuotationDate).IsRequired();
        builder.Property(q => q.ValidUntil).HasColumnType("date");
        builder.Property(q => q.TotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(q => q.Status).HasConversion<short>().IsRequired().HasDefaultValue(QuotationStatus.Draft);
        builder.Property(q => q.ConvertedOrderNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(q => q.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(q => q.CreatedAt).IsRequired();
        builder.Property(q => q.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，生成机制见 specs/015-erp-purchase design.md §3.6）
        builder.HasIndex(q => q.QuotationNo).IsUnique();

        // 客户外键：不级联删除（客户只停用不删除）
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(q => q.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
