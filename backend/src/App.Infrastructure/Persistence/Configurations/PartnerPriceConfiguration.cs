using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 客户协议价表映射配置（表名 PartnerPrices，唯一索引 (PartnerId, ProductId) 与外键限制删除）
/// </summary>
internal sealed class PartnerPriceConfiguration : IEntityTypeConfiguration<PartnerPrice>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PartnerPrice> builder)
    {
        builder.ToTable("PartnerPrices");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Price).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(p => p.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // 「客户 × 商品」唯一（单一协议价；多维定价属范围外）
        builder.HasIndex(p => new { p.PartnerId, p.ProductId }).IsUnique();
        builder.HasIndex(p => p.PartnerId);
        builder.HasIndex(p => p.ProductId);

        // 往来单位与商品均只停用不删除，故外键不级联删除
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(p => p.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}