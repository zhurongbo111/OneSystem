using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 采购单表映射配置（表名 PurchaseOrders，单号唯一索引；外键不级联删除，业务对象只停用 / 作废不删除）
/// </summary>
internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");
        builder.HasKey(p => p.Id);

        // 长度取自 OrderFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(p => p.OrderNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(p => p.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(p => p.OrderDate).IsRequired();
        builder.Property(p => p.TotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(p => p.SettlementStatus).HasConversion<short>().IsRequired();
        builder.Property(p => p.Status).HasConversion<short>().IsRequired();
        builder.Property(p => p.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，见 design.md §3.6）
        builder.HasIndex(p => p.OrderNo).IsUnique();
    }
}
