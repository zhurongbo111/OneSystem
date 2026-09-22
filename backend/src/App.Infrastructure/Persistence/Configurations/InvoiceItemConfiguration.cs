using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 发票关联单据明细表映射配置（表名 InvoiceItems，外键不级联删除，见 design.md §2.2）。
/// 单据号 / 单据日期 / 单据总额为快照字段；<c>(OrderType, OrderId)</c> 复合索引服务「已开票金额」聚合（高频）。
/// </summary>
internal sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.OrderType).HasConversion<short>().IsRequired();
        builder.Property(i => i.OrderNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(i => i.OrderDate).IsRequired();
        builder.Property(i => i.OrderTotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.Amount).IsRequired().HasColumnType("numeric(18,2)");

        builder.HasIndex(i => i.InvoiceId);

        // 「已开票金额」聚合按 (OrderType, OrderId) 查询，建复合索引而非单列索引
        builder.HasIndex(i => new { i.OrderType, i.OrderId });

        // 主表外键：不级联删除（发票只作废不删除）
        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(i => i.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}