using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 采购退货单表映射配置（表名 PurchaseReturns，单号唯一索引；外键不级联删除，业务对象只作废不删除）
/// </summary>
internal sealed class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("PurchaseReturns");
        builder.HasKey(r => r.Id);

        // 长度取自 OrderFieldConstraints（单据域单一来源），与各 RequestValidator 保持一致
        builder.Property(r => r.ReturnNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(r => r.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(r => r.ReturnDate).IsRequired();
        builder.Property(r => r.TotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(r => r.SettledAmount).IsRequired().HasColumnType("numeric(18,2)").HasDefaultValue(0m);
        builder.Property(r => r.Status).HasConversion<short>().IsRequired();
        builder.Property(r => r.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，见 design.md §3.1）
        builder.HasIndex(r => r.ReturnNo).IsUnique();
    }
}
