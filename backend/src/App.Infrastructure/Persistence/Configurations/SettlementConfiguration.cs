using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 收付款单表映射配置（表名 Settlements，单号唯一索引；外键不级联删除，业务对象只作废不删除）。
/// 长度取自 OrderFieldConstraints / PartnerFieldConstraints（单一来源，见 design.md §2.5）。
/// </summary>
internal sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlements");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SettlementNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(s => s.Type).HasConversion<short>().IsRequired();
        builder.Property(s => s.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(s => s.SettlementDate).IsRequired();
        builder.Property(s => s.TotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(s => s.Method).HasConversion<short>().IsRequired();
        builder.Property(s => s.Status).HasConversion<short>().IsRequired();
        builder.Property(s => s.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，机制同采购 / 销售单，见 design.md 与 erp-purchase §3.6）
        builder.HasIndex(s => s.SettlementNo).IsUnique();

        // 往来单位外键：不级联删除（往来单位只停用不删除）
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(s => s.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
